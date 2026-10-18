using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Service.Helpers.DTOs.Airport;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.DTOs.Notification;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.DTOs.Seat;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>One traveller's whole journey through the API, the way the frontend pages call it.</summary>
public class EndToEndTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EndToEndTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchBookPayAndCancel_ThenTheAdminSeesIt()
    {
        await _factory.GetSeedAsync();
        var client = _factory.CreateClient();

        // Register / sign in
        var account = await client.RegisterAsync(UniqueEmail("traveller"));
        client.WithToken(account.AccessToken);

        // Home page: airports for the search form
        var airports = await (await client.GetAsync("/api/airports")).ReadAsync<List<AirportDto>>();
        var from = airports.Single(a => a.City == "Baku").Code;
        var to = airports.Single(a => a.City == "London").Code;

        // Flight results
        var departureDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10));
        var search = await (await client.PostAsJsonAsync("/api/flights/search", new FlightSearchRequestDto
        {
            Legs = { new FlightSearchLegDto { OriginCode = from, DestinationCode = to, Date = departureDate } },
            CabinClass = CabinClass.Economy,
            Passengers = 1
        }, Json)).ReadAsync<List<FlightSearchLegResultDto>>();
        var flight = search.Single().Flights.Single();

        // Seat selection, baggage and meal pages
        var seat = (await (await client.GetAsync($"/api/flights/{flight.Id}/seats?cabinClass=Economy")).ReadAsync<SeatMapDto>())
            .Seats.First(s => s.IsAvailable);
        var baggage = (await (await client.GetAsync("/api/booking-options/baggage")).ReadAsync<List<BaggageOptionDto>>()).First();
        var meal = (await (await client.GetAsync("/api/booking-options/meals")).ReadAsync<List<MealOptionDto>>()).First();

        // Payment page: promo code and price summary
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/promo-codes/AEROVA10")).StatusCode);
        var request = new CreateBookingRequestDto
        {
            TripType = TripType.OneWay,
            Legs = { new BookingLegRequestDto { FlightId = flight.Id, CabinClass = CabinClass.Economy } },
            Passengers =
            {
                new BookingPassengerRequestDto
                {
                    FirstName = "Leyla", LastName = "Mammadova", DateOfBirth = new DateOnly(1995, 5, 12), Gender = Gender.Female,
                    NationalityCode = "AZ", DocumentType = DocumentType.Passport, DocumentNumber = "C07654321",
                    DocumentIssuingCountry = "AZ", DocumentExpiryDate = new DateOnly(2031, 5, 1),
                    BaggageOptionId = baggage.Id, MealOptionId = meal.Id,
                    Seats = { new PassengerSeatRequestDto { FlightId = flight.Id, FlightSeatId = seat.Id } }
                }
            },
            ContactEmail = account.User.Email,
            ContactPhone = "+994501234567",
            PromoCode = "AEROVA10"
        };
        var quote = await (await client.PostAsJsonAsync("/api/bookings/quote", request, Json)).ReadAsync<PriceBreakdownDto>();

        // Booking, then payment
        var created = await client.PostAsJsonAsync("/api/bookings", request, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.ReadAsync<BookingDto>();
        Assert.Equal(quote, booking.Price);

        var paid = await (await client.PostAsJsonAsync("/api/payments", new PaymentRequestDto
        {
            Pnr = booking.Pnr, Method = PaymentMethod.Card, CardholderName = "Leyla Mammadova",
            CardNumber = "5555 5555 5555 4444", ExpiryMonth = 1, ExpiryYear = DateTime.UtcNow.Year + 3, Cvv = "321"
        }, Json)).ReadAsync<BookingDto>();
        Assert.Equal(BookingStatus.Confirmed, paid.Status);
        Assert.Equal(baggage.Name, paid.Passengers.Single().Tickets.Single().BaggageName);

        // My Trips, notifications and manage booking
        Assert.Equal(booking.Pnr, (await (await client.GetAsync("/api/bookings/my")).ReadAsync<List<BookingSummaryDto>>()).Single().Pnr);
        Assert.Equal(2, await (await client.GetAsync("/api/notifications/unread-count")).ReadAsync<int>());
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync($"/api/bookings/{booking.Pnr}?lastName=Mammadova")).StatusCode);

        // Cancellation: 10 days before an Economy flight is inside the 20% fee band of the seeded rules
        var cancellationQuote = await (await client.GetAsync($"/api/bookings/{booking.Pnr}/cancellation-quote?lastName=Mammadova")).ReadAsync<CancellationQuoteDto>();
        Assert.Equal(paid.Price.Total, cancellationQuote.AmountPaid);
        Assert.Equal(20m, cancellationQuote.FeePercent);

        var cancelled = await (await client.PostAsJsonAsync($"/api/bookings/{booking.Pnr}/cancel", new { lastName = "Mammadova", reason = "Trip postponed" }, Json)).ReadAsync<BookingDto>();
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Equal(cancellationQuote.RefundAmount, cancelled.Cancellation!.RefundAmount);
        Assert.Equal(RefundStatus.Pending, cancelled.Cancellation.RefundStatus);

        var notifications = await (await client.GetAsync("/api/notifications")).ReadAsync<List<NotificationDto>>();
        Assert.Contains(notifications, n => n.Type == NotificationType.BookingCancelled);

        // The seat is free again and the admin sees the cancelled booking
        Assert.True((await (await client.GetAsync($"/api/flights/{flight.Id}/seats")).ReadAsync<SeatMapDto>()).Seats.Single(s => s.Id == seat.Id).IsAvailable);
        var admin = await _factory.CreateAdminClientAsync();
        var row = (await (await admin.GetAsync($"/api/admin/bookings?pnr={booking.Pnr}")).ReadAsync<List<AdminBookingRowDto>>()).Single();
        Assert.Equal(BookingStatus.Cancelled, row.Status);
        Assert.Equal("Leyla Mammadova", row.LeadPassenger);
    }
}
