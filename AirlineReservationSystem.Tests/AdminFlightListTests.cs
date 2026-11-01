using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Flight;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>The admin flight list: filters, seat counts, the journeys with a transfer, and who may read them.</summary>
public class AdminFlightListTests : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Monday = new(2026, 12, 14);
    private readonly ApiFactory _factory;

    public AdminFlightListTests(ApiFactory factory) => _factory = factory;

    private async Task PrepareAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        await seeder.SeedAsync(generateFlights: false);
        await seeder.SeedNetworkAsync(generateFlights: false);
        var generation = scope.ServiceProvider.GetRequiredService<IFlightGenerationService>();
        await generation.GenerateAsync(null, Monday, Monday.AddDays(6));
    }

    private async Task<HttpClient> AdminAsync() => (await _factory.CreateAccountAsync(UserRole.Admin)).Client;

    [Fact]
    public async Task TheList_IsFilteredByRouteMonthAndType_AndShowsSeatCounts()
    {
        await PrepareAsync();
        var admin = await AdminAsync();

        var rows = await (await admin.GetAsync("/api/admin/flights?origin=GYD&destination=ATH&month=12&year=2026&flightType=direct&take=100"))
            .ReadAsync<List<AdminFlightRowDto>>();

        Assert.NotEmpty(rows);
        Assert.All(rows, r =>
        {
            Assert.Equal(("GYD", "ATH"), (r.OriginCode, r.DestinationCode));
            Assert.Equal(0, r.Stops);
            Assert.Equal("direct", r.FlightType);
            Assert.Equal(97, r.SeatsTotal); // the aircraft's seats, one inventory row each
            Assert.InRange(r.SeatsAvailable, 0, r.SeatsTotal);
            Assert.Equal(new[] { CabinClass.Economy, CabinClass.Comfort, CabinClass.Business }.OrderBy(c => c), r.Fares.Keys.OrderBy(c => c));
        });
        Assert.All(rows, r => Assert.Equal(12, r.DepartureAt.Month));
    }

    [Fact]
    public async Task TheList_CanBeFilteredByAircraftAndActiveStatus()
    {
        await PrepareAsync();
        var admin = await AdminAsync();
        var all = await (await admin.GetAsync("/api/admin/flights?origin=GYD&destination=ATH&take=100")).ReadAsync<List<AdminFlightRowDto>>();
        var aircraft = all.First().AircraftId;

        var onAircraft = await (await admin.GetAsync($"/api/admin/flights?origin=GYD&destination=ATH&aircraftId={aircraft}&take=100")).ReadAsync<List<AdminFlightRowDto>>();
        Assert.All(onAircraft, r => Assert.Equal(aircraft, r.AircraftId));

        var active = await (await admin.GetAsync("/api/admin/flights?origin=GYD&destination=ATH&active=true&take=100")).ReadAsync<List<AdminFlightRowDto>>();
        // Another test of this class may have switched a schedule off: the active rows are exactly the ones whose schedule is on.
        Assert.Equal(all.Count(r => r.ScheduleActive), active.Count);
        Assert.All(active, r => Assert.True(r.ScheduleActive));
    }

    [Fact]
    public async Task ADeactivatedSchedule_HidesItsFutureFlightsFromSearch_AndFromBooking_ButKeepsThemInTheList()
    {
        await PrepareAsync();
        var admin = await AdminAsync();
        var rows = await (await admin.GetAsync("/api/admin/flights?origin=GYD&destination=ATH&take=100")).ReadAsync<List<AdminFlightRowDto>>();
        var target = rows.First(r => r.FlightScheduleId is not null);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var schedule = await db.FlightSchedules.SingleAsync(s => s.Id == target.FlightScheduleId);
            schedule.IsActive = false;
            await db.SaveChangesAsync();
        }

        var inactive = await (await admin.GetAsync("/api/admin/flights?origin=GYD&destination=ATH&active=false&take=100")).ReadAsync<List<AdminFlightRowDto>>();
        Assert.Contains(inactive, r => r.Id == target.Id);
        Assert.All(inactive, r => Assert.False(r.ScheduleActive));

        var day = DateOnly.FromDateTime(target.DepartureAt);
        var search = new FlightSearchRequestDto { Legs = { new FlightSearchLegDto { OriginCode = "GYD", DestinationCode = "ATH", Date = day } } };
        var found = (await (await _factory.CreateClient().PostAsJsonAsync("/api/flights/search", search, Json)).ReadAsync<List<FlightSearchLegResultDto>>()).Single();
        Assert.DoesNotContain(found.Flights, f => f.Id == target.Id);
    }

    [Fact]
    public async Task TheJourneys_BetweenTwoAirports_AreListedWithTheirTransferAndLegs()
    {
        await PrepareAsync();
        var admin = await AdminAsync();

        var journeys = await (await admin.GetAsync($"/api/admin/flights/journeys?origin=GYD&destination=JTR&from={Monday:yyyy-MM-dd}&to={Monday.AddDays(6):yyyy-MM-dd}"))
            .ReadAsync<List<AdminJourneyDto>>();

        Assert.NotEmpty(journeys);
        Assert.All(journeys, j =>
        {
            Assert.Equal("ATH", j.TransferCode);
            Assert.Equal(2, j.Legs.Count);
            Assert.Equal(("GYD", "ATH"), (j.Legs[0].OriginCode, j.Legs[0].DestinationCode));
            Assert.Equal(("ATH", "JTR"), (j.Legs[1].OriginCode, j.Legs[1].DestinationCode));
            Assert.InRange(j.TransferMinutes, 90, 480);
            Assert.Equal((int)(j.Legs[1].ArrivalAt - j.Legs[0].DepartureAt).TotalMinutes, j.DurationMinutes);
            Assert.Equal(j.Legs[0].DepartureAt, j.DepartureAt);
        });
    }

    [Fact]
    public async Task TheJourneys_RefuseAPeriodLongerThanSixtyTwoDays()
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync($"/api/admin/flights/journeys?origin=GYD&destination=JTR&from={Monday:yyyy-MM-dd}&to={Monday.AddDays(100):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task TheSearch_MatchesAnAirportCode_AndAFlightNumber()
    {
        await PrepareAsync();
        var admin = await AdminAsync();

        var byCode = await (await admin.GetAsync("/api/admin/flights?search=ath&take=100")).ReadAsync<List<AdminFlightRowDto>>();
        Assert.NotEmpty(byCode);
        Assert.All(byCode, r => Assert.True(r.OriginCode == "ATH" || r.DestinationCode == "ATH"));

        var first = byCode.First();
        var byNumber = await (await admin.GetAsync($"/api/admin/flights?search={Uri.EscapeDataString(first.FlightNumber)}&take=100")).ReadAsync<List<AdminFlightRowDto>>();
        Assert.Contains(byNumber, r => r.Id == first.Id);
    }

    [Fact]
    public async Task TheListAndJourneys_AreForAdminsOnly()
    {
        await PrepareAsync();
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/flights?take=5")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/admin/flights/journeys?origin=GYD&destination=JTR&from={Monday:yyyy-MM-dd}&to={Monday:yyyy-MM-dd}")).StatusCode);

        var customer = _factory.CreateClient();
        customer.WithToken((await customer.RegisterAsync()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/flights?take=5")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/admin/flights/journeys?origin=GYD&destination=JTR&from={Monday:yyyy-MM-dd}&to={Monday:yyyy-MM-dd}")).StatusCode);
    }
}
