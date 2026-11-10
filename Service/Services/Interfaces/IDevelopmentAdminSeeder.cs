using Service.Helpers.DTOs.Seed;

namespace Service.Services.Interfaces;

public interface IDevelopmentAdminSeeder
{
    /// <summary>Creates the Admin account if no account uses the email yet. Existing accounts are never changed.</summary>
    Task<DevelopmentAdminResultDto> EnsureAdminAsync(string? email, string? password, CancellationToken cancellationToken = default);
}
