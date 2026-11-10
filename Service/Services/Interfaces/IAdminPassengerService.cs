using Domain.Enums;
using Service.Helpers.DTOs.Passenger;

namespace Service.Services.Interfaces;

/// <summary>Admin "Passengers" page: travellers grouped by travel document.</summary>
public interface IAdminPassengerService
{
    /// <summary>Filters by name, email or document number, and optionally by active/inactive.</summary>
    Task<IReadOnlyList<AdminPassengerDto>> GetAllAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default);

    Task<AdminPassengerDetailsDto> GetDetailsAsync(DocumentType documentType, string documentNumber, CancellationToken cancellationToken = default);
}
