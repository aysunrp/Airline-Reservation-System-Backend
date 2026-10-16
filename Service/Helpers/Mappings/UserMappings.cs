using Domain.Entities;
using Service.Helpers.DTOs.Account;

namespace Service.Helpers.Mappings;

public static class UserMappings
{
    public static UserDto ToUserDto(this User user)
        => new(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString());
}
