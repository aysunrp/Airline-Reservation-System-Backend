using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class SavedPassenger : BaseEntity
{
    public int UserId { get; set; }
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

    public User User { get; set; } = null!;
}
