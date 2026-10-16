using Domain.Enums;

namespace Service.Helpers.DTOs.Passenger;

public record SavedPassengerDto(
    int Id,
    string FirstName,
    string LastName,
    string? Relationship,
    DateOnly? DateOfBirth,
    Gender? Gender,
    string? NationalityCode,
    DocumentType? DocumentType,
    string? DocumentNumber,
    string? DocumentIssuingCountry,
    DateOnly? DocumentExpiryDate);

public class SavedPassengerRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? NationalityCode { get; set; }
    public DocumentType? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentIssuingCountry { get; set; }
    public DateOnly? DocumentExpiryDate { get; set; }
}
