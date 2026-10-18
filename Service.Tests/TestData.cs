using Domain.Entities;
using Domain.Enums;

namespace Service.Tests;

/// <summary>
/// A small, self-contained flight network: Baku (GYD) - London (LHR) outbound on 10 Nov 2026 via Istanbul,
/// and a direct return on 17 Nov 2026. Each flight has two Business and three Economy seats.
/// Flight seat ids: outbound 101-105, return 201-205 (101/201 = 1A Business, 103/203 = 11A Economy).
/// </summary>
internal sealed class TestData
{
    public static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime OutboundDeparture = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime ReturnDeparture = new(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);

    public Airport Baku { get; } = new() { Id = 1, Code = "GYD", Name = "Heydar Aliyev", City = "Baku", CountryCode = "AZ", TimeZoneId = "Asia/Baku" };
    public Airport London { get; } = new() { Id = 2, Code = "LHR", Name = "Heathrow", City = "London", CountryCode = "GB", TimeZoneId = "Europe/London" };
    public Airport Istanbul { get; } = new() { Id = 3, Code = "IST", Name = "Istanbul", City = "Istanbul", CountryCode = "TR", TimeZoneId = "Europe/Istanbul" };

    public Flight Outbound { get; }
    public Flight Return { get; }
    public List<FlightSeat> Seats { get; } = new();

    public TestData()
    {
        var aircraft = new Aircraft { Id = 1, Name = "AEROVA Horizon", Model = "A350-900", RegistrationNumber = "4K-AV08" };
        var layout = new[]
        {
            new AircraftSeat { Id = 1, Aircraft = aircraft, RowNumber = 1, Letter = "A", CabinClass = CabinClass.Business },
            new AircraftSeat { Id = 2, Aircraft = aircraft, RowNumber = 1, Letter = "B", CabinClass = CabinClass.Business },
            new AircraftSeat { Id = 3, Aircraft = aircraft, RowNumber = 11, Letter = "A", CabinClass = CabinClass.Economy },
            new AircraftSeat { Id = 4, Aircraft = aircraft, RowNumber = 11, Letter = "B", CabinClass = CabinClass.Economy },
            new AircraftSeat { Id = 5, Aircraft = aircraft, RowNumber = 11, Letter = "C", CabinClass = CabinClass.Economy }
        };

        var outboundRoute = new Route { Id = 1, OriginAirportId = Baku.Id, Origin = Baku, DestinationAirportId = London.Id, Destination = London };
        var returnRoute = new Route { Id = 2, OriginAirportId = London.Id, Origin = London, DestinationAirportId = Baku.Id, Destination = Baku };

        Outbound = new Flight
        {
            Id = 1, FlightNumber = "AV 205", Route = outboundRoute, RouteId = 1, Aircraft = aircraft, AircraftId = 1,
            DepartureAt = OutboundDeparture, ArrivalAt = OutboundDeparture.AddMinutes(535)
        };
        Outbound.Segments.Add(new FlightSegment { SequenceNo = 1, Origin = Baku, Destination = Istanbul, DepartureAt = OutboundDeparture, ArrivalAt = OutboundDeparture.AddMinutes(170) });
        Outbound.Segments.Add(new FlightSegment { SequenceNo = 2, Origin = Istanbul, Destination = London, DepartureAt = OutboundDeparture.AddMinutes(250), ArrivalAt = OutboundDeparture.AddMinutes(535) });
        Outbound.Fares.Add(new FlightFare { CabinClass = CabinClass.Economy, BasePrice = 365 });
        Outbound.Fares.Add(new FlightFare { CabinClass = CabinClass.Business, BasePrice = 820 });

        Return = new Flight
        {
            Id = 2, FlightNumber = "AV 102", Route = returnRoute, RouteId = 2, Aircraft = aircraft, AircraftId = 1,
            DepartureAt = ReturnDeparture, ArrivalAt = ReturnDeparture.AddMinutes(340)
        };
        Return.Segments.Add(new FlightSegment { SequenceNo = 1, Origin = London, Destination = Baku, DepartureAt = ReturnDeparture, ArrivalAt = ReturnDeparture.AddMinutes(340) });
        Return.Fares.Add(new FlightFare { CabinClass = CabinClass.Economy, BasePrice = 420 });
        Return.Fares.Add(new FlightFare { CabinClass = CabinClass.Business, BasePrice = 890 });

        foreach (var (flight, offset) in new[] { (Outbound, 100), (Return, 200) })
        {
            foreach (var seat in layout)
            {
                Seats.Add(new FlightSeat { Id = offset + seat.Id, FlightId = flight.Id, Flight = flight, AircraftSeatId = seat.Id, AircraftSeat = seat });
            }
        }
    }

    public FlightSeat Seat(int id) => Seats.Single(s => s.Id == id);

    public Flight Flight(int id) => id == Outbound.Id ? Outbound : Return;

    public static BaggageOption[] BaggageOptions =>
    [
        new() { Id = 1, Code = "bag10", Name = "+10 kg", WeightKg = 10, Price = 30 },
        new() { Id = 2, Code = "bag20", Name = "+20 kg", WeightKg = 20, Price = 50 },
        new() { Id = 3, Code = "bag30", Name = "+30 kg", WeightKg = 30, Price = 70, IsActive = false }
    ];

    public static MealOption[] MealOptions =>
    [
        new() { Id = 1, Code = "classic", Name = "Classic Dining", Price = 18 },
        new() { Id = 2, Code = "signature", Name = "Signature Dining", Price = 35 }
    ];

    public FakeAirportRepository Airports() => new(Baku, London, Istanbul);

    public FakeFlightRepository Flights() => new(Outbound, Return);

    public FakeFlightSeatRepository FlightSeats() => new(Seats.ToArray());
}
