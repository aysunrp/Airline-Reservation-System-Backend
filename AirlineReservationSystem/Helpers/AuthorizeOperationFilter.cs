using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AirlineReservationSystem.Helpers;

/// <summary>
/// Marks only the endpoints that need a token with the Bearer requirement (the lock icon in Swagger UI)
/// and documents their 401/403 responses; anonymous endpoints stay unlocked.
/// </summary>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public const string SchemeName = "Bearer";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (!metadata.OfType<IAuthorizeData>().Any() || metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing or invalid token" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "The account is not allowed to use this endpoint" });

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = SchemeName }
            }] = Array.Empty<string>()
        });
    }
}
