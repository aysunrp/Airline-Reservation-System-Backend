using FluentValidation;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Validators;

public class RouteRequestValidator : AbstractValidator<RouteRequestDto>
{
    public RouteRequestValidator()
    {
        RuleFor(x => x.OriginCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.DestinationCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.DestinationCode)
            .Must((r, destination) => !string.Equals(r.OriginCode, destination, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Origin and destination must be different.");
        RuleFor(x => x.DistanceKm).InclusiveBetween(1, 20000);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(1, 24 * 60);
    }
}
