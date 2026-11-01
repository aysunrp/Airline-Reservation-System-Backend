using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Service.Helpers.DTOs.Airport;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>A destination without a direct flight (Santorini) is searched, priced and booked as one journey through its hub (Athens).</summary>
public class ConnectionJourneyTests : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Monday = new(2026, 12, 14);
    private readonly ApiFactory _factory;

    public ConnectionJourneyTests(ApiFactory factory) => _factory = factory;

    private async Task PrepareAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        await seeder.SeedAsync(generateFlights: false);
        await seeder.SeedNetworkAsync(generateFlights: false);
        var generation = scope.ServiceProvider.GetRequiredService<IFlightGenerationService>();
        await generation.GenerateAsync(null, Monday, Monday.AddDays(2)); // Monday to Wednesday
    }

    private async Task<FlightSearchLegResultDto> SearchAsync(string from, string to, DateOnly date, int passengers = 1)
    {
        var request = new FlightSearchRequestDto { Passengers = passengers, Legs = { new FlightSearchLegDto { OriginCode = from, DestinationCode = to, Date = date } } };
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/flights/search", request, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadAsync<List<FlightSearchLegResultDto>>()).Single();
    }

    /// <summary>A booking whose passenger has the first free Economy seat of every flight: booking asks for one seat on each flight of the journey.</summary>
    private async Task<CreateBookingRequestDto> BookingWithSeatsAsync(params int[] flightIds)
    {
        var request = Booking(flightIds);
        foreach (var id in flightIds)
        {
            var map = await (await _factory.CreateClient().GetAsync($"/api/flights/{id}/seats")).ReadAsync<Service.Helpers.DTOs.Seat.SeatMapDto>();
            var seat = map.Seats.First(x => x.IsAvailable && x.CabinClass == CabinClass.Economy);
            request.Passengers[0].Seats.Add(new PassengerSeatRequestDto { FlightId = id, FlightSeatId = seat.Id });
        }

        return request;
    }

    private static CreateBookingRequestDto Booking(params int[] flightIds) => new()
    {
        TripType = TripType.OneWay,
        Legs = flightIds.Select(id => new BookingLegRequestDto { FlightId = id, CabinClass = CabinClass.Economy }).ToList(),
        Passengers =
        {
            new BookingPassengerRequestDto
            {
                Title = "Ms", FirstName = "Aysun", LastName = "Panahli", DateOfBirth = new DateOnly(1990, 3, 1), Gender = Gender.Female,
                NationalityCode = "AZ", DocumentType = DocumentType.Passport, DocumentNumber = "C01234567",
                DocumentIssuingCountry = "AZ", DocumentExpiryDate = new DateOnly(2032, 1, 1)
            }
        },
        ContactEmail = "aysun@example.com",
        ContactPhone = "+994501234567"
    };

    [Fact]
    public async Task Santorini_HasNoDirectFlightFromBaku_ButAConnectionThroughAthens_WithTheFaresAdded()
    {
        await PrepareAsync();

        var result = await SearchAsync("GYD", "JTR", Monday);

        Assert.Empty(result.Flights);
        var connection = Assert.Single(result.Connections);
        Assert.Equal(("ATH", 2), (connection.TransferCode, connection.Flights.Count));
        Assert.InRange(connection.TransferMinutes, 90, 480);
        Assert.Equal(("GYD", "ATH"), (connection.Flights[0].OriginCode, connection.Flights[0].DestinationCode));
        Assert.Equal(("ATH", "JTR"), (connection.Flights[1].OriginCode, connection.Flights[1].DestinationCode));
        Assert.True(connection.Flights[1].DepartureAt >= connection.Flights[0].ArrivalAt.AddMinutes(90));
        Assert.Equal(connection.Flights[0].Fares[CabinClass.Economy] + connection.Flights[1].Fares[CabinClass.Economy], connection.Fares[CabinClass.Economy]);
        Assert.True(connection.Fares[CabinClass.Business] > connection.Fares[CabinClass.Comfort] && connection.Fares[CabinClass.Comfort] > connection.Fares[CabinClass.Economy]);
        Assert.Equal((int)(connection.ArrivalAt - connection.DepartureAt).TotalMinutes, connection.DurationMinutes);
    }

    [Fact]
    public async Task ADirectDestination_StillShowsItsDirectFlight_AndTheRouteCheckKnowsBothKindsOfJourney()
    {
        await PrepareAsync();

        var athens = await SearchAsync("GYD", "ATH", Monday);

        Assert.Single(athens.Flights);
        Assert.Empty(athens.Connections);
        var client = _factory.CreateClient();
        Assert.True((await (await client.GetAsync("/api/airports/route-check?origin=GYD&destination=JTR")).ReadAsync<RouteCheckDto>()).Exists); // by a connection
        Assert.True((await (await client.GetAsync("/api/airports/route-check?origin=GYD&destination=ATH")).ReadAsync<RouteCheckDto>()).Exists); // direct
        Assert.True((await (await client.GetAsync("/api/airports/route-check?origin=GYD&destination=MIA")).ReadAsync<RouteCheckDto>()).Exists); // by a connection through New York
        Assert.False((await (await client.GetAsync("/api/airports/route-check?origin=BJV&destination=JTR")).ReadAsync<RouteCheckDto>()).Exists); // two stops away
    }

    [Fact]
    public async Task ThePriceCalendar_CountsADayWithAConnection()
    {
        await PrepareAsync();

        var days = await (await _factory.CreateClient().GetAsync($"/api/flights/lowest-fares?originCode=GYD&destinationCode=JTR&from={Monday:yyyy-MM-dd}&to={Monday.AddDays(2):yyyy-MM-dd}&passengers=1"))
            .ReadAsync<List<DayFareDto>>();

        // Baku - Athens and Athens - Santorini both fly on Monday, Wednesday, Friday and Sunday: Monday and Wednesday of the three days asked for.
        Assert.Equal(new[] { Monday, Monday.AddDays(2) }, days.Select(d => d.Date).ToArray());
    }

    [Fact]
    public async Task AConnectionIsBookedAsOneJourney_WithOnePnr_AndFlightsThatDoNotJoinAreRefused()
    {
        await PrepareAsync();
        var connection = Assert.Single((await SearchAsync("GYD", "JTR", Monday)).Connections);
        var client = _factory.CreateClient();
        var ids = connection.Flights.Select(f => f.Id).ToArray();

        var quote = await client.PostAsJsonAsync("/api/bookings/quote", Booking(ids), Json);
        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);

        var created = await client.PostAsJsonAsync("/api/bookings", await BookingWithSeatsAsync(ids), Json);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var booking = await created.ReadJsonAsync();
        Assert.False(string.IsNullOrWhiteSpace(booking.GetProperty("pnr").GetString()));
        var legs = booking.GetProperty("legs").EnumerateArray().ToList();
        Assert.Equal(("GYD", "ATH"), (legs[0].GetProperty("originCode").GetString(), legs[0].GetProperty("destinationCode").GetString()));
        Assert.Equal(new[] { 1, 1 }, legs.Select(l => l.GetProperty("journeyNo").GetInt32()).ToArray());
        Assert.Equal(new[] { 1, 2 }, legs.Select(l => l.GetProperty("legOrder").GetInt32()).ToArray());

        // Wednesday's Athens - Santorini flight leaves two days after Monday's Baku - Athens one lands: too long a wait for one journey.
        var wednesday = Assert.Single((await SearchAsync("ATH", "JTR", Monday.AddDays(2))).Flights);
        var tooLong = await client.PostAsJsonAsync("/api/bookings/quote", Booking(ids[0], wednesday.Id), Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLong.StatusCode);

        // Two flights that do not join: the second one must leave from the airport the first one landed at.
        var elsewhere = Assert.Single((await SearchAsync("ATH", "JMK", Monday)).Flights);
        var wrongOrder = await client.PostAsJsonAsync("/api/bookings/quote", Booking(elsewhere.Id, ids[0]), Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongOrder.StatusCode);
    }

    [Fact]
    public async Task ARoundTripOfTwoConnections_KeepsTheOutboundAndReturnJourneysApart()
    {
        await PrepareAsync();
        var client = _factory.CreateClient();
        var outbound = Assert.Single((await SearchAsync("GYD", "JTR", Monday)).Connections);
        var back = await SearchAsync("JTR", "GYD", Monday.AddDays(2)); // Wednesday
        var returning = Assert.Single(back.Connections);

        var request = await BookingWithSeatsAsync(outbound.Flights.Select(f => f.Id).Concat(returning.Flights.Select(f => f.Id)).ToArray());
        request.TripType = TripType.RoundTrip;
        for (var i = 0; i < request.Legs.Count; i++)
        {
            request.Legs[i].JourneyNo = i < 2 ? 1 : 2;
        }

        var created = await client.PostAsJsonAsync("/api/bookings", request, Json);

        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var legs = (await created.ReadJsonAsync()).GetProperty("legs").EnumerateArray().Select(l => l.GetProperty("journeyNo").GetInt32()).ToArray();
        Assert.Equal(new[] { 1, 1, 2, 2 }, legs);
    }

    [Theory]
    [InlineData("JFK", 14)] // Monday: Istanbul - New York flies Monday, Wednesday, Friday, Sunday
    [InlineData("LAX", 15)] // Tuesday: Istanbul - Los Angeles flies Tuesday, Thursday, Saturday
    public async Task TheFarDestinations_AreReachedThroughIstanbul_NotByAnInventedDirectFlight(string code, int day)
    {
        await PrepareAsync();

        var result = await SearchAsync("GYD", code, new DateOnly(2026, 12, day));

        Assert.Empty(result.Flights);
        Assert.All(result.Connections, c => Assert.Equal("IST", c.TransferCode));
        var connection = result.Connections.First();
        Assert.Equal(("GYD", "IST"), (connection.Flights[0].OriginCode, connection.Flights[0].DestinationCode));
        Assert.Equal(("IST", code), (connection.Flights[1].OriginCode, connection.Flights[1].DestinationCode));
        Assert.InRange(connection.TransferMinutes, 90, 480);

        // And no invented direct flight on the way back either.
        var back = await SearchAsync(code, "GYD", new DateOnly(2026, 12, day + 1));
        Assert.Empty(back.Flights);
    }

    [Fact]
    public async Task ARouteThatLeftTheNetwork_IsSwitchedOffWithoutBeingDeleted()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
            await seeder.SeedAsync(generateFlights: false);
            var db = scope.ServiceProvider.GetRequiredService<Repository.Data.AppDbContext>();
            var gyd = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Airports, a => a.Code == "GYD");
            var jfk = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Airports, a => a.Code == "JFK");
            db.Routes.Add(new Domain.Entities.Route { OriginAirportId = gyd.Id, DestinationAirportId = jfk.Id, DistanceKm = 9361, DurationMinutes = 815 });
            await db.SaveChangesAsync();
            await seeder.SeedNetworkAsync(generateFlights: false);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Repository.Data.AppDbContext>();
            var route = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Routes, r => r.Origin.Code == "GYD" && r.Destination.Code == "JFK");
            Assert.False(route.IsActive); // still there for the history, no longer sold
        }
    }
}
