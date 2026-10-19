using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

/// <summary>Accepted documents and minimum validity for a trip, combined over all of its flights.</summary>
public sealed record TravelDocumentRequirement(IReadOnlyList<DocumentType> Documents, int MinValidityDays);

public interface ITravelDocumentPolicy
{
    /// <summary>Expects every flight's segments (or route) to have their airports loaded.</summary>
    TravelDocumentRequirement GetRequirement(IReadOnlyList<Flight> flights, string nationalityCode);

    /// <summary>Throws a business-rule error for the first passenger whose document is not accepted or expires too early.</summary>
    void Check(IReadOnlyList<Flight> flights, IReadOnlyList<BookingPassengerRequestDto> passengers, DateOnly lastTravelDate);
}
