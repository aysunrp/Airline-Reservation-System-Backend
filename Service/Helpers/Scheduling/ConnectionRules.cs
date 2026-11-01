using Domain.Enums;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Scheduling;

/// <summary>
/// The rules of a journey with a transfer, shared by the search (which offers connections) and by booking (which accepts them), so a connection
/// that is listed can always be booked. A connection is two or more flights of one journey: each lands where the next one departs, and the
/// wait in between is at least the minimum connection time and at most the longest wait AEROVA sells.
/// </summary>
public static class ConnectionRules
{
    /// <summary>The shortest wait between landing and the next departure.</summary>
    public const int MinTransferMinutes = 90;

    /// <summary>The longest wait AEROVA sells as one journey.</summary>
    public const int MaxTransferMinutes = 8 * 60;

    /// <summary>Flights in one journey (two transfers at most).</summary>
    public const int MaxFlightsPerJourney = 3;

    /// <summary>Connections the search offers for one request, best first.</summary>
    public const int MaxConnectionsPerSearch = 30;

    public static bool IsValidTransfer(DateTime arrivalUtc, DateTime nextDepartureUtc)
    {
        var wait = (nextDepartureUtc - arrivalUtc).TotalMinutes;
        return wait >= MinTransferMinutes && wait <= MaxTransferMinutes;
    }

    /// <summary>
    /// The journey of each leg of a booking request (1 outbound, 2 return), or null when the numbering is wrong. Legs that do not say
    /// (0): a one-way booking is all journey 1; a round trip of two flights is journey 1 and 2.
    /// </summary>
    public static IReadOnlyList<int>? JourneyNumbers(TripType tripType, IReadOnlyList<BookingLegRequestDto> legs)
    {
        if (legs.Count == 0)
        {
            return null;
        }

        if (legs.All(l => l.JourneyNo == 0))
        {
            return tripType switch
            {
                TripType.OneWay => legs.Select(_ => 1).ToList(),
                TripType.RoundTrip when legs.Count == 2 => new[] { 1, 2 },
                _ => null
            };
        }

        var numbers = legs.Select(l => l.JourneyNo).ToList();
        for (var i = 0; i < numbers.Count; i++)
        {
            var allowed = tripType == TripType.OneWay ? numbers[i] == 1 : numbers[i] is 1 or 2;
            var ordered = i == 0 ? numbers[i] == 1 : numbers[i] == numbers[i - 1] || numbers[i] == numbers[i - 1] + 1;
            if (!allowed || !ordered)
            {
                return null;
            }
        }

        if (tripType == TripType.RoundTrip && !numbers.Contains(2))
        {
            return null;
        }

        return numbers.GroupBy(n => n).Any(g => g.Count() > MaxFlightsPerJourney) ? null : numbers;
    }
}
