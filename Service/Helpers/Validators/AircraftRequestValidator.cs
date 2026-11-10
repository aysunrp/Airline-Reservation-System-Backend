using System.Text.RegularExpressions;
using FluentValidation;
using Service.Helpers.DTOs.Aircraft;

namespace Service.Helpers.Validators;

public class AircraftRequestValidator : AbstractValidator<AircraftRequestDto>
{
    private const int MaxSeatsPerCabin = 600;
    private static readonly Regex RegistrationPattern = new("^[A-Za-z0-9-]{3,20}$");

    public AircraftRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RegistrationNumber).NotEmpty()
            .Must(r => RegistrationPattern.IsMatch((r ?? string.Empty).Trim()))
            .WithMessage("Enter a valid registration number, e.g. 4K-AV01.");
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.BusinessSeats).InclusiveBetween(0, MaxSeatsPerCabin);
        RuleFor(x => x.ComfortSeats).InclusiveBetween(0, MaxSeatsPerCabin);
        RuleFor(x => x.EconomySeats).InclusiveBetween(0, MaxSeatsPerCabin);
        RuleFor(x => x.BusinessSeats + x.ComfortSeats + x.EconomySeats).GreaterThan(0)
            .WithName("Seats").WithMessage("An aircraft needs at least one seat.");
    }
}
