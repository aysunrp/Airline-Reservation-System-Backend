using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Service.Helpers;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// The usual answers (401 for nobody, 403 for the wrong role), plus an audit entry when a signed-in account tries something it is
/// not allowed to do. Only the method and path are written, never the query string or the body.
/// </summary>
public sealed class AuditingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            var audit = (IAuditLogService)context.RequestServices.GetService(typeof(IAuditLogService))!;
            await audit.RecordAsync(context.User.ToAuditActor(), AuditActions.AccessDenied, "Endpoint", $"{context.Request.Method} {context.Request.Path}", null, context.RequestAborted);
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
