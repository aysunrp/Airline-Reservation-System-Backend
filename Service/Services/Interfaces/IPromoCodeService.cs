using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface IPromoCodeService
{
    /// <summary>Returns the promo code when it can be used now; otherwise throws a BusinessRuleException.</summary>
    Task<PromoCodeDto> ValidateAsync(string code, CancellationToken cancellationToken = default);
}
