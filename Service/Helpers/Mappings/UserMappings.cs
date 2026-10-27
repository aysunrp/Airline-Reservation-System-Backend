using Domain.Entities;
using Service.Helpers.DTOs.Account;

namespace Service.Helpers.Mappings;

public static class UserMappings
{
    public static UserDto ToUserDto(this User user)
        => new(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString(), user.EmailVerifiedAt is not null);

    public static ProfileDto ToProfileDto(this User user)
        => new(user.Id, user.Title, user.FirstName, user.LastName, user.DateOfBirth, user.NationalityCode, user.Email, user.Phone, user.EmailVerifiedAt is not null);
}
