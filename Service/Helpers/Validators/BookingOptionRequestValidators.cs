using FluentValidation;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Validators;

public class BaggageOptionRequestValidator : AbstractValidator<BaggageOptionRequestDto>
{
    public BaggageOptionRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(BookingOptionCode.Pattern).WithMessage(BookingOptionCode.Message);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.WeightKg).InclusiveBetween(1, 100);
        RuleFor(x => x.Price).InclusiveBetween(0, 10_000);
    }
}

public class MealOptionRequestValidator : AbstractValidator<MealOptionRequestDto>
{
    public MealOptionRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(BookingOptionCode.Pattern).WithMessage(BookingOptionCode.Message);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).InclusiveBetween(0, 10_000);
    }
}

internal static class BookingOptionCode
{
    public const string Pattern = @"^\s*[A-Za-z0-9-]{2,30}\s*$";
    public const string Message = "Use 2-30 letters, digits or dashes for the code.";
}
