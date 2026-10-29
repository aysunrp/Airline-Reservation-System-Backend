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
        RuleFor(x => x.SortBy).Must(s => s is "price" or "departure").WithMessage("Sort by \"price\" or \"departure\".");
        RuleFor(x => x.Limit).InclusiveBetween(1, Math.Max(1, options.Value.MaxResults)).When(x => x.Limit is not null);
        RuleFor(x => x.From)
            .Must(d => d >= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("The travel date cannot be in the past.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("The last day cannot be before the first day.");
        RuleFor(x => x.To)
            .Must((x, to) => to.DayNumber - x.From.DayNumber < maxDays)
            .WithMessage($"A search can cover at most {maxDays} days.");
    }
}
