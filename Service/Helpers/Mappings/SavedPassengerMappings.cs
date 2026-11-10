using Domain.Entities;
using Service.Helpers.DTOs.Passenger;

namespace Service.Helpers.Mappings;

public static class SavedPassengerMappings
{
    public static SavedPassengerDto ToDto(this SavedPassenger passenger)
        => new(passenger.Id, passenger.FirstName, passenger.LastName, passenger.Relationship,
            passenger.DateOfBirth, passenger.Gender, passenger.NationalityCode, passenger.DocumentType,
            passenger.DocumentNumber, passenger.DocumentIssuingCountry, passenger.DocumentExpiryDate);

    /// <summary>Copies the request onto a new or existing entity (shared by create and update).</summary>
    public static void ApplyTo(this SavedPassengerRequestDto request, SavedPassenger passenger)
    {
        passenger.FirstName = request.FirstName.Trim();
        passenger.LastName = request.LastName.Trim();
        passenger.Relationship = string.IsNullOrWhiteSpace(request.Relationship) ? null : request.Relationship.Trim();
        passenger.DateOfBirth = request.DateOfBirth;
        passenger.Gender = request.Gender;
        passenger.NationalityCode = request.NationalityCode;
        passenger.DocumentType = request.DocumentType;
        passenger.DocumentNumber = string.IsNullOrWhiteSpace(request.DocumentNumber) ? null : request.DocumentNumber.Trim().ToUpperInvariant();
        passenger.DocumentIssuingCountry = request.DocumentIssuingCountry;
        passenger.DocumentExpiryDate = request.DocumentExpiryDate;
    }
}
