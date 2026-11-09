namespace Service.Helpers.DTOs.Account;

public record ProfileDto(
    int Id,
    string? Title,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? NationalityCode,
    string Email,
    string? Phone,
    bool EmailVerified,
    string? FatherName = null,
    string? PassportNumber = null,
    string? NationalIdNumber = null);

public class UpdateProfileRequestDto
{
    public string? Title { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? NationalityCode { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>Full number including the country code, e.g. +994508343943.</summary>
    public string? Phone { get; set; }

    /// <summary>The father's name. Not sent (null): left as it is. Sent empty: removed.</summary>
    public string? FatherName { get; set; }

    /// <summary>The passport number. Not sent (null): left as it is. Sent empty: removed. Spaces and dashes are ignored.</summary>
    public string? PassportNumber { get; set; }

    /// <summary>The identity card number. Not sent (null): left as it is. Sent empty: removed. Spaces and dashes are ignored.</summary>
    public string? NationalIdNumber { get; set; }
}
