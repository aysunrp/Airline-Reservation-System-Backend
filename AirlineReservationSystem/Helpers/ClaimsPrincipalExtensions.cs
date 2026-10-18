using System.Security.Claims;
using Service.Helpers.Exceptions;

namespace AirlineReservationSystem.Helpers;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id from the token ("sub" is mapped to NameIdentifier).</summary>
    public static int GetUserId(this ClaimsPrincipal user)
        => user.GetUserIdOrNull() ?? throw new UnauthorizedException("The token does not identify a user.");

    /// <summary>The user's id when a valid token was sent; null for guests.</summary>
    public static int? GetUserIdOrNull(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
