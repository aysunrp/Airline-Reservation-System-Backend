using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class SavedPassenger : BaseEntity
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string NationalityCode { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string DocumentIssuingCountry { get; set; } = string.Empty;
    public DateOnly? DocumentExpiryDate { get; set; }

    public User User { get; set; } = null!;
}
