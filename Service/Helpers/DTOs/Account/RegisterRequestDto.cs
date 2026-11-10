namespace Service.Helpers.DTOs.Account;

public class RegisterRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Language of the e-mail: az (the site is Azerbaijani only; anything else gives az).</summary>
    public string? Language { get; set; }
}
