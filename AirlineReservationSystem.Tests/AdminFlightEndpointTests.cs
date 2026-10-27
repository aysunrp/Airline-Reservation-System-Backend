using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Service.Helpers.DTOs.Aircraft;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Seat;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class AdminFlightEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminFlightEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Admin, int AircraftId)> SetUpAsync()
    {
        await _factory.GetSeedAsync();
        var admin = await _factory.CreateSuperAdminClientAsync();
        var aircraft = await (await admin.GetAsync("/api/admin/aircraft")).ReadAsync<List<AircraftDto>>();
        return (admin, aircraft.Single().Id);
    }

    private static FlightRequestDto NewFlight(int aircraftId, string number, int daysAhead = 20) => new()
    {
        FlightNumber = number,
        OriginCode = "GYD",
        DestinationCode = "LHR",
        AircraftId = aircraftId,
        DepartureAt = DateTime.UtcNow.Date.AddDays(daysAhead).AddHours(8),
        ArrivalAt = DateTime.UtcNow.Date.AddDays(daysAhead).AddHours(13).AddMinutes(40),
        Fares = { [CabinClass.Economy] = 410, [CabinClass.Business] = 950 }
    };

    [Fact]
    public async Task Flight_CreateUpdateDelete()
    {
        var (admin, aircraftId) = await SetUpAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/flights", NewFlight(aircraftId, "av318"), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var flight = await created.ReadAsync<FlightResultDto>();
        Assert.Equal("AV 318", flight.FlightNumber);
        Assert.Equal($"/api/admin/flights/{flight.Id}", created.Headers.Location?.AbsolutePath);

        var inventory = await (await admin.GetAsync($"/api/admin/flights/{flight.Id}/seats")).ReadAsync<SeatInventoryDto>();
        Assert.Equal(5, inventory.Available);

        var delayed = NewFlight(aircraftId, "AV 318");
        delayed.Status = FlightStatus.Delayed;
        var updated = await admin.PutAsJsonAsync($"/api/admin/flights/{flight.Id}", delayed, Json);
        Assert.Equal(FlightStatus.Delayed, (await updated.ReadAsync<FlightResultDto>()).Status);

        Assert.Single(await (await admin.GetAsync("/api/admin/flights?status=Delayed")).ReadAsync<List<FlightResultDto>>());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/flights/{flight.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/flights/{flight.Id}")).StatusCode);
    }

    [Fact]
    public async Task Flight_InvalidRequests()
    {
        var (admin, aircraftId) = await SetUpAsync();

        var past = NewFlight(aircraftId, "AV 900", daysAhead: -2);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/flights", past, Json)).StatusCode);

        var comfort = NewFlight(aircraftId, "AV 901");
        comfort.Fares[CabinClass.Comfort] = 600; // the seeded aircraft has no Comfort seats
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync("/api/admin/flights", comfort, Json)).StatusCode);

        var noRoute = NewFlight(aircraftId, "AV 902");
        noRoute.OriginCode = "LHR";
        noRoute.DestinationCode = "GYD";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync("/api/admin/flights", noRoute, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/admin/flights", NewFlight(aircraftId, "AV 903"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/flights", NewFlight(aircraftId, "AV 903"), Json)).StatusCode);
    }

    [Fact]
    public async Task BookedFlight_CannotBeDeleted_AndBookedSeatsCannotBeBlocked()
    {
        var (admin, _) = await SetUpAsync();
        var seed = await _factory.GetSeedAsync();
        Assert.Equal(HttpStatusCode.Created, (await _factory.CreateClient().PostAsJsonAsync("/api/bookings", BookingEndpointTests.Request(seed, "11A"), Json)).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.DeleteAsync($"/api/admin/flights/{seed.FlightId}")).StatusCode);

        var heldSeat = await admin.PatchAsJsonAsync($"/api/admin/flights/{seed.FlightId}/seats/{seed.SeatIds["11A"]}", new SeatBlockRequestDto { Blocked = true }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, heldSeat.StatusCode);
    }

    [Fact]
    public async Task SeatInventory_BlockAndUnblock()
    {
        var (admin, _) = await SetUpAsync();
        var seed = await _factory.GetSeedAsync();
        var seatUrl = $"/api/admin/flights/{seed.FlightId}/seats/{seed.SeatIds["1B"]}";

        var blocked = await admin.PatchAsJsonAsync(seatUrl, new SeatBlockRequestDto { Blocked = true }, Json);
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        Assert.Equal(SeatStatus.Blocked, (await blocked.ReadAsync<InventorySeatDto>()).Status);

        var business = await (await admin.GetAsync($"/api/admin/flights/{seed.FlightId}/seats?cabinClass=Business")).ReadAsync<SeatInventoryDto>();
        Assert.Equal(1, business.Blocked);
        Assert.Equal(2, business.Seats.Count);

        Assert.Equal(SeatStatus.Available, (await (await admin.PatchAsJsonAsync(seatUrl, new SeatBlockRequestDto { Blocked = false }, Json)).ReadAsync<InventorySeatDto>()).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PatchAsJsonAsync(seatUrl, new SeatBlockRequestDto { Blocked = false }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PatchAsJsonAsync($"/api/admin/flights/{seed.FlightId}/seats/999999", new SeatBlockRequestDto { Blocked = true }, Json)).StatusCode);
    }

    [Fact]
    public async Task FlightList_SupportsPagingAndTheDayFilter()
    {
        var (admin, aircraftId) = await SetUpAsync();
        var first = await (await admin.PostAsJsonAsync("/api/admin/flights", NewFlight(aircraftId, "AV 931", 31), Json)).ReadAsync<FlightResultDto>();
        var second = await (await admin.PostAsJsonAsync("/api/admin/flights", NewFlight(aircraftId, "AV 932", 32), Json)).ReadAsync<FlightResultDto>();

        var day = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(31)).ToString("yyyy-MM-dd");
        var onDay = await (await admin.GetAsync($"/api/admin/flights?date={day}")).ReadAsync<List<FlightResultDto>>();
        Assert.Equal(first.Id, onDay.Single().Id);

        var pageOne = await (await admin.GetAsync("/api/admin/flights?search=AV 93&take=1")).ReadAsync<List<FlightResultDto>>();
        var pageTwo = await (await admin.GetAsync("/api/admin/flights?search=AV 93&take=1&skip=1")).ReadAsync<List<FlightResultDto>>();
        Assert.Equal(first.Id, pageOne.Single().Id);
        Assert.Equal(second.Id, pageTwo.Single().Id);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/flights?take=100000")).StatusCode);
    }
}
