using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Service.Helpers.DTOs.Passenger;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class AdminBookingEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminBookingEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<BookingDto> BookAsync(string seat, string lastName, string documentNumber, bool pay)
    {
        var seed = await _factory.GetSeedAsync();
        var client = _factory.CreateClient();
        var request = BookingEndpointTests.Request(seed, seat, lastName: lastName);
        request.Passengers[0].DocumentNumber = documentNumber;

        var booking = await (await client.PostAsJsonAsync("/api/bookings", request, Json)).ReadAsync<BookingDto>();
        if (!pay)
        {
            return booking;
        }

        var paid = await client.PostAsJsonAsync("/api/payments", new PaymentRequestDto { Pnr = booking.Pnr, Method = PaymentMethod.ApplePay }, Json);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        return await paid.ReadAsync<BookingDto>();
    }

    [Fact]
    public async Task Bookings_ListFiltersAndDetails()
    {
        var paid = await BookAsync("11A", "Mammadova", "C1111111", pay: true);
        await BookAsync("11B", "Huseynova", "C2222222", pay: false);
        var admin = await _factory.CreateAdminClientAsync();

        var all = await (await admin.GetAsync("/api/admin/bookings")).ReadAsync<List<AdminBookingRowDto>>();
        Assert.True(all.Count >= 2);

        var byName = await (await admin.GetAsync("/api/admin/bookings?passenger=mammadova")).ReadAsync<List<AdminBookingRowDto>>();
        var row = Assert.Single(byName);
        Assert.Equal(paid.Pnr, row.Pnr);
        Assert.Equal(new[] { "11A" }, row.Seats);
        Assert.Equal("Baku → London", row.Route);

        Assert.Single(await (await admin.GetAsync($"/api/admin/bookings?pnr={paid.Pnr[..4]}&status=Confirmed")).ReadAsync<List<AdminBookingRowDto>>());

        var details = await admin.GetAsync($"/api/admin/bookings/{paid.Pnr}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Equal(BookingStatus.Confirmed, (await details.ReadAsync<BookingDto>()).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/bookings/ZZZZZZ")).StatusCode);
    }

    [Fact]
    public async Task AdminCancel_WorksWithoutLastName_AndOnlyOnce()
    {
        var booking = await BookAsync("1A", "Ismayilov", "C3333333", pay: true);
        var admin = await _factory.CreateAdminClientAsync();

        var tooLong = await admin.PostAsJsonAsync($"/api/admin/bookings/{booking.Pnr}/cancel", new AdminCancelBookingRequestDto { Reason = new string('x', 501) }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        var cancelled = await admin.PostAsJsonAsync($"/api/admin/bookings/{booking.Pnr}/cancel", new AdminCancelBookingRequestDto { Reason = "Requested by phone" }, Json);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var result = await cancelled.ReadAsync<BookingDto>();
        Assert.Equal(BookingStatus.Cancelled, result.Status);
        // Business, 10 days ahead, 0% fee band: the whole amount is requested back.
        Assert.Equal(RefundStatus.Pending, result.Cancellation!.RefundStatus);
        Assert.Equal(result.Price.Total, result.Cancellation.RefundAmount);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/bookings/{booking.Pnr}/cancel", new AdminCancelBookingRequestDto(), Json)).StatusCode);
    }

    [Fact]
    public async Task Passengers_AreGroupedWithTripsAndDetails()
    {
        var paid = await BookAsync("11C", "Aliyev", "C4444444", pay: true);
        var admin = await _factory.CreateAdminClientAsync();
        var document = paid.Passengers.Single();

        var list = await (await admin.GetAsync("/api/admin/passengers?search=aliyev")).ReadAsync<List<AdminPassengerDto>>();
        var passenger = Assert.Single(list);
        Assert.Equal(1, passenger.Trips);
        Assert.True(passenger.IsActive);

        var details = await admin.GetAsync($"/api/admin/passengers/{passenger.DocumentType}/{passenger.DocumentNumber}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Equal(paid.Pnr, (await details.ReadAsync<AdminPassengerDetailsDto>()).Bookings.Single().Pnr);
        Assert.Equal(DocumentType.Passport, document.DocumentType);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/passengers/Passport/X0000000")).StatusCode);
    }
}
