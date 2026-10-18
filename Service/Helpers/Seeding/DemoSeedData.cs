using Domain.Enums;

namespace Service.Helpers.Seeding;

public sealed record AirportSeed(string Code, string Name, string City, string CountryCode, string TimeZoneId);

public sealed record AircraftSeed(string Name, string Model, string RegistrationNumber, int BusinessSeats, int ComfortSeats, int EconomySeats);

public sealed record RouteSeed(string OriginCode, string DestinationCode, int DistanceKm, int DurationMinutes);

public sealed record StopSeed(string AirportCode, int ArrivalOffsetMinutes, int LayoverMinutes);

/// <summary>Departure and arrival are local times at the two airports, as on the frontend's results page.</summary>
public sealed record ScheduleSeed(
    string FlightNumber,
    string OriginCode,
    string DestinationCode,
    string AircraftRegistration,
    TimeOnly Departure,
    TimeOnly Arrival,
    int ArrivalDayOffset,
    decimal Economy,
    decimal Comfort,
    decimal Business,
    StopSeed[]? Stops = null);

public sealed record BaggageSeed(string Code, string Name, int WeightKg, decimal Price);

public sealed record MealSeed(string Code, string Name, string Description, decimal Price);

public sealed record CancellationRuleSeed(CabinClass CabinClass, int MinDaysBeforeDeparture, decimal FeePercent);

/// <summary>
/// The fixed demo network. Airports, timetable, prices, baggage, meals, promo codes and fare rules are taken from
/// the frontend's mock data so both sides show the same thing; return flights use the outbound prices.
/// </summary>
public static class DemoSeedData
{
    /// <summary>Schedules start on this date so the seed does not depend on the day it runs.</summary>
    public static readonly DateOnly ScheduleValidFrom = new(2026, 1, 1);

    private const string B789 = "4K-AV01";
    private const string A350 = "4K-AV02";
    private const string A321 = "4K-AV03";
    private const string A320 = "4K-AV04";

    public static readonly IReadOnlyList<AirportSeed> Airports = new[]
    {
        new AirportSeed("GYD", "Heydar Aliyev International Airport", "Baku", "AZ", "Asia/Baku"),
        new AirportSeed("NAJ", "Nakhchivan International Airport", "Nakhchivan", "AZ", "Asia/Baku"),
        new AirportSeed("LHR", "Heathrow Airport", "London", "GB", "Europe/London"),
        new AirportSeed("CDG", "Paris Charles de Gaulle Airport", "Paris", "FR", "Europe/Paris"),
        new AirportSeed("DXB", "Dubai International Airport", "Dubai", "AE", "Asia/Dubai"),
        new AirportSeed("IST", "Istanbul Airport", "Istanbul", "TR", "Europe/Istanbul"),
        new AirportSeed("TBS", "Tbilisi International Airport", "Tbilisi", "GE", "Asia/Tbilisi"),
        new AirportSeed("VIE", "Vienna International Airport", "Vienna", "AT", "Europe/Vienna")
    };

    // 12 Business, 25 Comfort and 60 Economy seats give the frontend's seat map: rows 1-3, 5-9 and 11-20.
    public static readonly IReadOnlyList<AircraftSeed> Aircraft = new[]
    {
        new AircraftSeed("AEROVA 787-9", "787-9", B789, 12, 25, 60),
        new AircraftSeed("AEROVA A350-900", "A350-900", A350, 12, 25, 60),
        new AircraftSeed("AEROVA A321neo", "A321neo", A321, 12, 25, 60),
        new AircraftSeed("AEROVA A320neo", "A320neo", A320, 12, 25, 60)
    };

    public static readonly IReadOnlyList<RouteSeed> Routes = new[]
    {
        new RouteSeed("GYD", "LHR", 3950, 340), new RouteSeed("LHR", "GYD", 3950, 330),
        new RouteSeed("GYD", "CDG", 3800, 345), new RouteSeed("CDG", "GYD", 3800, 315),
        new RouteSeed("GYD", "DXB", 1830, 155), new RouteSeed("DXB", "GYD", 1830, 155),
        new RouteSeed("GYD", "IST", 1760, 195), new RouteSeed("IST", "GYD", 1760, 165),
        new RouteSeed("GYD", "NAJ", 380, 75), new RouteSeed("NAJ", "GYD", 380, 75)
    };

    private static TimeOnly T(int hour, int minute) => new(hour, minute);

    // Stop times are minutes after departure; layovers are the frontend's.
    public static readonly IReadOnlyList<ScheduleSeed> Schedules = new[]
    {
        new ScheduleSeed("AV 101", "GYD", "LHR", B789, T(9, 30), T(13, 10), 0, 420, 560, 890),
        new ScheduleSeed("AV 205", "GYD", "LHR", A350, T(14, 20), T(19, 15), 0, 365, 490, 820, new[] { new StopSeed("IST", 190, 80) }),
        new ScheduleSeed("AV 401", "GYD", "LHR", B789, T(15, 0), T(19, 30), 0, 420, 560, 890),
        new ScheduleSeed("AV 318", "GYD", "LHR", A321, T(19, 45), T(23, 25), 0, 455, 575, 950),
        new ScheduleSeed("AV 612", "GYD", "LHR", A320, T(7, 15), T(13, 30), 0, 310, 430, 720,
            new[] { new StopSeed("TBS", 70, 70), new StopSeed("VIE", 360, 110) }),
        new ScheduleSeed("AV 220", "GYD", "CDG", A350, T(8, 10), T(11, 55), 0, 390, 520, 860),
        new ScheduleSeed("AV 226", "GYD", "CDG", A321, T(15, 30), T(20, 40), 0, 340, 470, 790, new[] { new StopSeed("IST", 190, 95) }),
        new ScheduleSeed("AV 232", "GYD", "CDG", A320, T(20, 5), T(23, 50), 0, 410, 620, 880),
        new ScheduleSeed("AV 724", "GYD", "CDG", A321, T(22, 40), T(7, 35), 1, 285, 395, 660,
            new[] { new StopSeed("IST", 190, 150), new StopSeed("VIE", 490, 125) }),
        new ScheduleSeed("AV 310", "GYD", "DXB", A320, T(6, 45), T(9, 20), 0, 210, 290, 480),
        new ScheduleSeed("AV 316", "GYD", "DXB", A321, T(13, 15), T(15, 55), 0, 245, 330, 520),
        new ScheduleSeed("AV 322", "GYD", "DXB", B789, T(18, 50), T(21, 30), 0, 280, 380, 560),
        new ScheduleSeed("AV 501", "GYD", "IST", A320, T(7, 55), T(10, 10), 0, 185, 250, 400),
        new ScheduleSeed("AV 508", "GYD", "IST", A321, T(12, 40), T(15, 5), 0, 200, 275, 430),
        new ScheduleSeed("AV 515", "GYD", "IST", A350, T(21, 10), T(23, 25), 0, 220, 310, 470),
        new ScheduleSeed("AV 801", "GYD", "NAJ", A320, T(7, 20), T(8, 35), 0, 75, 110, 190),
        new ScheduleSeed("AV 805", "GYD", "NAJ", A321, T(13, 40), T(14, 55), 0, 85, 120, 205),
        new ScheduleSeed("AV 809", "GYD", "NAJ", A320, T(19, 15), T(20, 30), 0, 95, 135, 220),
        new ScheduleSeed("AV 802", "NAJ", "GYD", A320, T(9, 5), T(10, 20), 0, 75, 110, 190),
        new ScheduleSeed("AV 806", "NAJ", "GYD", A321, T(15, 30), T(16, 45), 0, 85, 120, 205),

        // Return flights to Baku, priced like the outbound ones.
        new ScheduleSeed("AV 102", "LHR", "GYD", B789, T(10, 15), T(19, 45), 0, 420, 560, 890),
        new ScheduleSeed("AV 402", "LHR", "GYD", B789, T(21, 0), T(6, 30), 1, 420, 560, 890),
        new ScheduleSeed("AV 221", "CDG", "GYD", A350, T(13, 10), T(21, 25), 0, 390, 520, 860),
        new ScheduleSeed("AV 311", "DXB", "GYD", A320, T(10, 30), T(13, 5), 0, 210, 290, 480),
        new ScheduleSeed("AV 317", "DXB", "GYD", A321, T(17, 0), T(19, 35), 0, 245, 330, 520),
        new ScheduleSeed("AV 502", "IST", "GYD", A320, T(11, 30), T(15, 15), 0, 185, 250, 400),
        new ScheduleSeed("AV 509", "IST", "GYD", A321, T(16, 30), T(20, 15), 0, 200, 275, 430)
    };

    public static readonly IReadOnlyList<BaggageSeed> BaggageOptions = new[]
    {
        new BaggageSeed("bag10", "+10 kg", 10, 35),
        new BaggageSeed("bag20", "+20 kg", 20, 55),
        new BaggageSeed("bag30", "+30 kg", 30, 75)
    };

    public static readonly IReadOnlyList<MealSeed> MealOptions = new[]
    {
        new MealSeed("classic", "Classic Dining", "A carefully prepared classic meal served with seasonal sides.", 18),
        new MealSeed("wellness", "Wellness Dining", "A fresh and balanced selection prepared with wholesome ingredients.", 25),
        new MealSeed("signature", "Signature Dining", "A premium chef-inspired dining experience with elevated presentation and ingredients.", 35)
    };

    public static readonly IReadOnlyList<(string Code, int DiscountPercent)> PromoCodes = new[]
    {
        ("AEROVA10", 10), ("FLYAEROVA", 15), ("WELCOME20", 20)
    };

    // Closer to departure than the last band means no refund, so those bands have no row.
    public static readonly IReadOnlyList<CancellationRuleSeed> CancellationRules = new[]
    {
        new CancellationRuleSeed(CabinClass.Economy, 30, 20), new CancellationRuleSeed(CabinClass.Economy, 14, 40),
        new CancellationRuleSeed(CabinClass.Comfort, 30, 10), new CancellationRuleSeed(CabinClass.Comfort, 7, 25),
        new CancellationRuleSeed(CabinClass.Comfort, 1, 50),
        new CancellationRuleSeed(CabinClass.Business, 7, 0), new CancellationRuleSeed(CabinClass.Business, 1, 15)
    };
}
