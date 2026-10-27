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
    bool EmailVerified);

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
}
