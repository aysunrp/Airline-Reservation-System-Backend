using FluentValidation;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Validators;

public class LowestFaresRequestValidator : AbstractValidator<LowestFaresRequestDto>
{
    /// <summary>Two months of days fit a calendar grid and keep one request reasonable.</summary>
    public const int MaxDays = 62;

    public LowestFaresRequestValidator()
    {
        RuleFor(x => x.OriginCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.DestinationCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.DestinationCode)
            .Must((request, destination) => !string.Equals(request.OriginCode, destination, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Origin and destination must be different.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("The last day cannot be before the first day.");
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber < MaxDays)
            .WithName("To")
            .WithMessage($"A price calendar covers at most {MaxDays} days.");
        RuleFor(x => x.CabinClass).IsInEnum();
        RuleFor(x => x.Passengers).InclusiveBetween(1, CreateBookingRequestValidator.MaxPassengers);
    }
}
