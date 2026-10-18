using FluentValidation;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Validators;

public class FlightSearchRequestValidator : AbstractValidator<FlightSearchRequestDto>
{
    // Upper bound for a multi-city search; keeps a single request reasonable.
    private const int MaxLegs = 6;

    public FlightSearchRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Legs).NotEmpty().Must(l => l.Count <= MaxLegs)
            .WithMessage($"A search can contain at most {MaxLegs} flights.");

        RuleFor(x => x.CabinClass).IsInEnum();
        RuleFor(x => x.Passengers).InclusiveBetween(1, 9);

        RuleForEach(x => x.Legs).ChildRules(leg =>
        {
            leg.RuleFor(l => l.OriginCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
            leg.RuleFor(l => l.DestinationCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
            leg.RuleFor(l => l.DestinationCode)
                .Must((l, destination) => !string.Equals(l.OriginCode, destination, StringComparison.OrdinalIgnoreCase))
                .WithMessage("Origin and destination must be different.");
            leg.RuleFor(l => l.Date)
                .Must(d => d >= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
                .WithMessage("The travel date cannot be in the past.");
        });
    }
}
