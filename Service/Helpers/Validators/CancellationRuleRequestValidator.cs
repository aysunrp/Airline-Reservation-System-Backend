using FluentValidation;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Validators;

public class CancellationRuleRequestValidator : AbstractValidator<CancellationRuleRequestDto>
{
    public CancellationRuleRequestValidator()
    {
        RuleFor(x => x.CabinClass).IsInEnum();
        RuleFor(x => x.MinDaysBeforeDeparture).InclusiveBetween(0, 365);
        RuleFor(x => x.FeePercent).InclusiveBetween(0, 100).When(x => x.FeePercent is not null);
    }
}
