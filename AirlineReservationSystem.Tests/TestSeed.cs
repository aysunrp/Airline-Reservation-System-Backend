using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;

namespace AirlineReservationSystem.Tests;

/// <summary>Ids of the small flight network written into the test database.</summary>
public sealed record SeedData(
    int FlightId,
    DateTime Departure,
    IReadOnlyDictionary<string, int> SeatIds,
    int BaggageOptionId,
    int MealOptionId);

/// <summary>
/// Writes one Baku → London flight ten days from now (2 Business seats 1A–1B, 3 Economy seats 11A–11C,
/// Economy 420 / Business 890), baggage and meal options, the AEROVA10 promo code and the Economy cancellation rules.
/// </summary>
internal static class TestSeed
{
    public static async Task<SeedData> CreateAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var baku = new Airport { Code = "GYD", Name = "Heydar Aliyev International", City = "Baku", CountryCode = "AZ", TimeZoneId = "Asia/Baku" };
        var london = new Airport { Code = "LHR", Name = "Heathrow", City = "London", CountryCode = "GB", TimeZoneId = "Europe/London" };
        var route = new Route { Origin = baku, Destination = london, DistanceKm = 3950, DurationMinutes = 340 };

        var aircraft = new Aircraft { Name = "AEROVA Horizon", Model = "A350-900", RegistrationNumber = "4K-AV08" };
        foreach (var (row, letter, cabin) in new[]
                 {
                     (1, "A", CabinClass.Business), (1, "B", CabinClass.Business),
                     (11, "A", CabinClass.Economy), (11, "B", CabinClass.Economy), (11, "C", CabinClass.Economy)
                 })
        {
            aircraft.Seats.Add(new AircraftSeat { RowNumber = row, Letter = letter, CabinClass = cabin });
        }

        var departure = DateTime.UtcNow.Date.AddDays(10).AddHours(9);
        var flight = new Flight
        {
            FlightNumber = "AV 101", Route = route, Aircraft = aircraft,
            DepartureAt = departure, ArrivalAt = departure.AddMinutes(340)
        };
        flight.Segments.Add(new FlightSegment { SequenceNo = 1, Origin = baku, Destination = london, DepartureAt = departure, ArrivalAt = departure.AddMinutes(340) });
        flight.Fares.Add(new FlightFare { CabinClass = CabinClass.Economy, BasePrice = 420 });
        flight.Fares.Add(new FlightFare { CabinClass = CabinClass.Business, BasePrice = 890 });
        foreach (var seat in aircraft.Seats)
        {
            flight.Seats.Add(new FlightSeat { AircraftSeat = seat });
        }

        var baggage = new BaggageOption { Code = "bag20", Name = "+20 kg", WeightKg = 20, Price = 55 };
        var meal = new MealOption { Code = "classic", Name = "Classic Dining", Description = "A classic meal.", Price = 18 };

        db.AddRange(flight, baggage, meal,
            new BaggageOption { Code = "bag10", Name = "+10 kg", WeightKg = 10, Price = 35 },
            new PromoCode { Code = "AEROVA10", DiscountPercent = 10 },
            new PromoCode { Code = "EXPIRED", DiscountPercent = 50, ValidTo = DateTime.UtcNow.AddDays(-1) },
            new CancellationRule { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 7, FeePercent = 20 },
            new CancellationRule { CabinClass = CabinClass.Business, MinDaysBeforeDeparture = 1, FeePercent = 0 });
        await db.SaveChangesAsync();

        return new SeedData(
            flight.Id,
            departure,
            flight.Seats.ToDictionary(s => $"{s.AircraftSeat.RowNumber}{s.AircraftSeat.Letter}", s => s.Id),
            baggage.Id,
            meal.Id);
    }
}
