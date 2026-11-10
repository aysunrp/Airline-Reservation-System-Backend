using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// A signed token proves who the account was when it was issued. This checks it still holds: the account exists, is active and has
/// the role the token claims, so a deactivation or a changed role takes effect on the next request instead of when the token expires.
/// The answer comes from the Service layer (<see cref="IUserStateService"/>), which remembers it for a few seconds.
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
        var state = await ((IUserStateService)services.GetService(typeof(IUserStateService))!).GetAsync(userId.Value, context.HttpContext.RequestAborted);

        var claimedRole = context.Principal!.FindFirstValue(ClaimTypes.Role);
        if (state is null || !state.IsActive || !string.Equals(state.Role.ToString(), claimedRole, StringComparison.Ordinal))
        {
            context.Fail("The account was changed or disabled. Sign in again.");
            return;
        }

        // "Sign out everywhere" and a changed password end every token issued before that moment.
        if (state.SessionsValidFrom is { } validFrom && context.SecurityToken is System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt
            && jwt.ValidFrom < validFrom)
        {
            context.Fail("The session was ended. Sign in again.");
            return;
        }

        // A token that carries a session is only good while that session is open (it can be ended on its own).
        var sessionKey = context.Principal.FindFirstValue(Service.Services.TokenService.SessionClaim);
        if (!string.IsNullOrEmpty(sessionKey))
        {
            var sessions = (IAccountSessionService)services.GetService(typeof(IAccountSessionService))!;
            if (!await sessions.IsOpenAsync(userId.Value, sessionKey, context.HttpContext.RequestAborted))
            {
                context.Fail("The session was ended. Sign in again.");
            }
        }
    }
}
