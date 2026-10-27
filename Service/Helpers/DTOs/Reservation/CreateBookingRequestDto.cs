using Domain.Enums;

namespace Service.Helpers.DTOs.Reservation;

public class CreateBookingRequestDto
{
    public TripType TripType { get; set; }

    /// <summary>Flights in travel order: one for one-way, outbound + return for round trip, one per city pair for multi-city.</summary>
    public List<BookingLegRequestDto> Legs { get; set; } = new();

    public List<BookingPassengerRequestDto> Passengers { get; set; } = new();

    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string? PromoCode { get; set; }

    /// <summary>The page language (en, az, ru) for the e-mail that confirms the booking.</summary>
    public string? Language { get; set; }
}

public class BookingLegRequestDto
{
    public int FlightId { get; set; }
    public CabinClass CabinClass { get; set; }
}

public class BookingPassengerRequestDto
{
    /// <summary>Mr, Ms, Mrs or Mx.</summary>
    public string Title { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    /// <summary>Optional, as on the passenger form.</summary>
    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string NationalityCode { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string DocumentIssuingCountry { get; set; } = string.Empty;
    public DateOnly? DocumentExpiryDate { get; set; }

    /// <summary>Set when the passenger was picked from the signed-in user's saved passengers.</summary>
    public int? SavedPassengerId { get; set; }

    /// <summary>Extra checked baggage for the whole journey (none when null).</summary>
    public int? BaggageOptionId { get; set; }

    /// <summary>Meal choice for the whole journey (none when null).</summary>
    public int? MealOptionId { get; set; }

    /// <summary>One seat per flight. Infants travel on an adult's lap and have no seat.</summary>
    public List<PassengerSeatRequestDto> Seats { get; set; } = new();
}

public class PassengerSeatRequestDto
{
    public int FlightId { get; set; }
    public int FlightSeatId { get; set; }
}
