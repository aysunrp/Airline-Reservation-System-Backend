using FluentValidation;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Validators;

public class FlightRequestValidator : AbstractValidator<FlightRequestDto>
{
    public FlightRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.FlightNumber).NotEmpty().Must(FlightNumber.IsValid).WithMessage("Use a flight number like AV 101.");
        RuleFor(x => x.OriginCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.DestinationCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.DestinationCode)
            .Must((f, destination) => !string.Equals(f.OriginCode, destination, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Origin and destination must be different.");
        RuleFor(x => x.AircraftId).GreaterThan(0);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.DepartureAt)
            .Must(d => d > timeProvider.GetUtcNow().UtcDateTime)
            .WithMessage("The departure must be in the future.");
        RuleFor(x => x.ArrivalAt).GreaterThan(x => x.DepartureAt).WithMessage("Arrival must be after departure.");
        RuleFor(x => x).Must(x => x.ArrivalAt - x.DepartureAt <= TimeSpan.FromHours(24))
            .WithName("ArrivalAt").WithMessage("A flight cannot be longer than 24 hours.");
        RuleFor(x => x.Fares).NotEmpty().WithMessage("Enter the base price for at least one cabin.");
        RuleForEach(x => x.Fares).ChildRules(fare =>
        {
            fare.RuleFor(f => f.Key).IsInEnum();
            fare.RuleFor(f => f.Value).GreaterThan(0).LessThan(100_000);
        });
    }
}
