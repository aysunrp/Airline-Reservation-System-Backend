using Service.Helpers.DTOs.Seed;

namespace Service.Services.Interfaces;

/// <summary>
/// The two ways an account becomes a SuperAdmin, both on the server and neither reachable through the API:
/// the one-time bootstrap at startup and the operator's promote command. Nobody is ever promoted silently.
/// </summary>
public interface ISuperAdminProvisioningService
{
    /// <summary>Creates the first SuperAdmin when there is no active one and no account uses the email. Existing accounts are never changed.</summary>
    Task<SuperAdminBootstrapResultDto> BootstrapAsync(string? email, string? password, CancellationToken cancellationToken = default);

    /// <summary>Makes an existing, active Admin with a confirmed e-mail address a SuperAdmin. Anything else is refused.</summary>
    Task<SuperAdminPromotionResultDto> PromoteAsync(string? email, CancellationToken cancellationToken = default);
}
