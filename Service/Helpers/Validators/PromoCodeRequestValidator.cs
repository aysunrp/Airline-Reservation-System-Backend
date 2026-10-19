using FluentValidation;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Validators;

public class PromoCodeRequestValidator : AbstractValidator<PromoCodeRequestDto>
{
    public PromoCodeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\s*[A-Za-z0-9]{3,30}\s*$")
            .WithMessage("Use 3-30 letters and digits for the promo code.");
        RuleFor(x => x.DiscountPercent).InclusiveBetween(1, 100);
        RuleFor(x => x.ValidTo).GreaterThan(x => x.ValidFrom)
            .When(x => x.ValidFrom is not null && x.ValidTo is not null)
            .WithMessage("The end of the validity window must be after its start.");
        RuleFor(x => x.MaxUses).GreaterThan(0).When(x => x.MaxUses is not null);
    }
}
