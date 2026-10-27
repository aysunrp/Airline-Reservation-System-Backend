using FluentValidation;
using Service.Helpers.DTOs.Payment;

namespace Service.Helpers.Validators;

public class CreateCheckoutSessionRequestValidator : AbstractValidator<CreateCheckoutSessionRequestDto>
{
    public CreateCheckoutSessionRequestValidator()
    {
        RuleFor(x => x.Pnr).NotEmpty().Matches("^[A-Za-z0-9]{6}$").WithMessage("Enter your booking reference.");
        RuleFor(x => x.LastName).MaximumLength(100);
    }
}
