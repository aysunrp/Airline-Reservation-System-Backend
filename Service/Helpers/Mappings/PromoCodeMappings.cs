using Domain.Entities;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Mappings;

public static class PromoCodeMappings
{
    public static AdminPromoCodeDto ToAdminDto(this PromoCode promoCode)
        => new(promoCode.Id, promoCode.Code, promoCode.DiscountPercent, promoCode.ValidFrom, promoCode.ValidTo,
            promoCode.MaxUses, promoCode.UsedCount, promoCode.IsActive);
}
