using FluentValidation;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Settings;

namespace Service.Helpers.Validators;

public class FlightRangeSearchRequestValidator : AbstractValidator<FlightRangeSearchRequestDto>
{
    public FlightRangeSearchRequestValidator(TimeProvider timeProvider, IOptions<VoiceSearchSettings> options)
    {
        var maxDays = Math.Max(1, options.Value.MaxRangeDays);

        RuleFor(x => x.OriginCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.DestinationCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.DestinationCode)
            .Must((x, destination) => !string.Equals(x.OriginCode, destination, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Origin and destination must be different.");
        RuleFor(x => x.CabinClass).IsInEnum();
        RuleFor(x => x.TimeOfDay).IsInEnum();
        RuleFor(x => x.Passengers).InclusiveBetween(1, CreateBookingRequestValidator.MaxPassengers);
        RuleFor(x => x.SortBy).Must(s => s is "price" or "departure" or "duration").WithMessage("Sort by \"price\", \"departure\" or \"duration\".");
        RuleFor(x => x.Stops).Must(s => s is null or "direct" or "connecting").WithMessage("Stops must be \"direct\" or \"connecting\".");
        RuleFor(x => x.From)
            .Must(d => d >= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("The travel date cannot be in the past.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("The last day cannot be before the first day.");
        RuleFor(x => x.To)
            .Must((x, to) => to.DayNumber - x.From.DayNumber < maxDays)
            .WithMessage($"A search can cover at most {maxDays} days.");
    }
}
