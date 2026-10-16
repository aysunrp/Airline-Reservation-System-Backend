namespace Service.Helpers.DTOs.Account;

public record UserDto(int Id, string Email, string FirstName, string LastName, string Role);

public record AuthResponseDto(string AccessToken, DateTime ExpiresAt, UserDto User);
