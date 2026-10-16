using FluentValidation;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Validators;

public class CancelBookingRequestValidator : AbstractValidator<CancelBookingRequestDto>
{
    public CancelBookingRequestValidator()
    {
        RuleFor(x => x.Pnr).NotEmpty().Matches("^[A-Za-z0-9]{6}$").WithMessage("Enter your booking reference.");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
