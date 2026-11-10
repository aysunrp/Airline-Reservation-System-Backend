using FluentValidation;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Validators;

public class FlightSearchRequestValidator : AbstractValidator<FlightSearchRequestDto>
{
    // One flight for a one-way search, two (outbound and return) for a round trip.
    private const int MaxLegs = 2;

    public FlightSearchRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Legs).NotEmpty().Must(l => l.Count <= MaxLegs)
            .WithMessage("A search is one-way (one flight) or round trip (outbound and return).");

        RuleFor(x => x.CabinClass).IsInEnum();
        RuleFor(x => x.Passengers).InclusiveBetween(1, CreateBookingRequestValidator.MaxPassengers);

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
