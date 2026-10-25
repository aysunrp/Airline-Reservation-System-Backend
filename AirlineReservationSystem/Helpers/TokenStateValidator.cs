using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Repository.Repositories.Interfaces;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// A signed token proves who the account was when it was issued. This checks it still holds: the account exists, is active and has
/// the role the token claims, so a deactivation or a changed role takes effect on the next request instead of when the token expires.
/// The answer is remembered for a few seconds (<see cref="IUserStateCache"/>).
/// </summary>
public static class TokenStateValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.GetUserIdOrNull();
        if (userId is null)
        {
            context.Fail("The token does not identify an account.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = (IUserStateCache)services.GetService(typeof(IUserStateCache))!;

        if (!cache.TryGet(userId.Value, out var state))
        {
            var users = (IUserRepository)services.GetService(typeof(IUserRepository))!;
            state = await users.GetStateAsync(userId.Value, context.HttpContext.RequestAborted);
            cache.Set(userId.Value, state);
        }

        var claimedRole = context.Principal!.FindFirstValue(ClaimTypes.Role);
        if (state is null || !state.IsActive || !string.Equals(state.Role.ToString(), claimedRole, StringComparison.Ordinal))
        {
            context.Fail("The account was changed or disabled. Sign in again.");
        }
    }
}
