using System.Text.RegularExpressions;
using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Validators;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequestDto>
{
    private const int MaxPassengers = 9;
    private const int MaxMultiCityLegs = 6;

    // Document formats used by the passenger details page.
    private static readonly Regex PassportNumber = new("^[A-Z0-9]{6,9}$");
    private static readonly Regex AzerbaijaniIdNumber = new(@"^(AA\d{7}|AZE\d{8})$");

    public CreateBookingRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.TripType).IsInEnum();
        RuleFor(x => x.Legs).NotEmpty();
        RuleFor(x => x.Legs.Count).Equal(1).When(x => x.TripType == TripType.OneWay)
            .WithMessage("A one-way booking has exactly one flight.");
        RuleFor(x => x.Legs.Count).Equal(2).When(x => x.TripType == TripType.RoundTrip)
            .WithMessage("A round-trip booking has an outbound and a return flight.");
        RuleFor(x => x.Legs.Count).InclusiveBetween(2, MaxMultiCityLegs).When(x => x.TripType == TripType.MultiCity)
            .WithMessage($"A multi-city booking has between 2 and {MaxMultiCityLegs} flights.");
        RuleFor(x => x.Legs).Must(l => l.Select(leg => leg.FlightId).Distinct().Count() == l.Count)
            .WithMessage("The same flight cannot be booked twice.");
        RuleForEach(x => x.Legs).ChildRules(leg =>
        {
            leg.RuleFor(l => l.FlightId).GreaterThan(0);
            leg.RuleFor(l => l.CabinClass).IsInEnum();
        });

        RuleFor(x => x.Passengers).NotEmpty().Must(p => p.Count <= MaxPassengers)
            .WithMessage($"A booking can have at most {MaxPassengers} passengers.");
        RuleForEach(x => x.Passengers).ChildRules(passenger =>
        {
            passenger.RuleFor(p => p.FirstName).NotEmpty().MaximumLength(100);
            passenger.RuleFor(p => p.LastName).NotEmpty().MaximumLength(100);
            passenger.RuleFor(p => p.DateOfBirth)
                .Must(d => d < DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
                .WithMessage("Enter a valid date of birth in the past.");
            passenger.RuleFor(p => p.Gender).IsInEnum();
            passenger.RuleFor(p => p.NationalityCode).Matches("^[A-Z]{2}$").WithMessage("Select your nationality.");
            passenger.RuleFor(p => p.DocumentType).IsInEnum();
            passenger.RuleFor(p => p.DocumentIssuingCountry).Matches("^[A-Z]{2}$").WithMessage("Select the issuing country.");
            passenger.RuleFor(p => p.DocumentNumber).NotEmpty()
                .Must((p, number) => IsValidDocumentNumber(p.DocumentType, number))
                .WithMessage("Enter a valid document number.");
            passenger.RuleForEach(p => p.Seats).ChildRules(seat =>
            {
                seat.RuleFor(s => s.FlightId).GreaterThan(0);
                seat.RuleFor(s => s.FlightSeatId).GreaterThan(0);
            });
        });

        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.ContactPhone).NotEmpty().Matches(@"^\+?[0-9 ]{6,20}$").WithMessage("Enter your phone number.");
        RuleFor(x => x.PromoCode).MaximumLength(30);
    }

    private static bool IsValidDocumentNumber(DocumentType type, string? number)
    {
        var normalized = (number ?? string.Empty).Trim().ToUpperInvariant();
        return type == DocumentType.NationalIdCard
            ? AzerbaijaniIdNumber.IsMatch(normalized)
            : PassportNumber.IsMatch(normalized);
    }
}
