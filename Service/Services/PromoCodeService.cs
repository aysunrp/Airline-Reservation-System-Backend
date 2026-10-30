using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Services;

public class PromoCodeService : IPromoCodeService
{
    private const string InvalidMessage = "Promo kod yanlışdır.";

    private readonly IPromoCodeRepository _promoCodeRepository;
    private readonly TimeProvider _timeProvider;

    public PromoCodeService(IPromoCodeRepository promoCodeRepository, TimeProvider timeProvider)
    {
        _promoCodeRepository = promoCodeRepository;
        _timeProvider = timeProvider;
    }

    public async Task<PromoCodeDto> ValidateAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new BusinessRuleException("Promo kodu daxil edin.");
        }

        var promoCode = await _promoCodeRepository.GetByCodeAsync(normalized, cancellationToken);
        if (promoCode is null || !promoCode.IsActive)
        {
            throw new BusinessRuleException(InvalidMessage);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (promoCode.ValidFrom > now || promoCode.ValidTo < now)
        {
            throw new BusinessRuleException("Bu promo kod hazırda etibarlı deyil.");
        }

        if (promoCode.MaxUses is not null && promoCode.UsedCount >= promoCode.MaxUses)
        {
            throw new BusinessRuleException("Bu promo kodun istifadə limiti dolub.");
        }

        return new PromoCodeDto(promoCode.Id, promoCode.Code, promoCode.DiscountPercent);
    }
}
