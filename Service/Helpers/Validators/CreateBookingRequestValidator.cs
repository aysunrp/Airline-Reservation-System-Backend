using System.Text.RegularExpressions;
using Domain.Enums;
using FluentValidation;
using Service.Helpers;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Scheduling;

namespace Service.Helpers.Validators;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequestDto>
{
    /// <summary>Travellers on one booking, infants included.</summary>
    public const int MaxPassengers = 9;

    // Document formats used by the passenger details page.
    private static readonly Regex PassportNumber = new("^[A-Z0-9]{6,9}$");
    private static readonly Regex AzerbaijaniIdNumber = new(@"^(AA\d{7}|AZE\d{8})$");

    public CreateBookingRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.TripType).IsInEnum();
        RuleFor(x => x.Legs).NotEmpty();
        RuleFor(x => x).Must(x => x.TripType is TripType.OneWay or TripType.RoundTrip && ConnectionRules.JourneyNumbers(x.TripType, x.Legs) is not null)
            .WithName("Legs").OverridePropertyName("Legs")
            .WithMessage("A one-way booking is one journey and a round trip an outbound and a return journey, each of up to three flights.");
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
            passenger.RuleFor(p => p.MiddleName).MaximumLength(100);
            passenger.RuleFor(p => p.LastName).NotEmpty().MaximumLength(100);
            passenger.RuleFor(p => p.DateOfBirth)
                .Must(d => d < DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
                .WithMessage("Enter a valid date of birth in the past.");
            passenger.RuleFor(p => p.Gender).IsInEnum();
            passenger.RuleFor(p => p.NationalityCode).Matches("^[A-Z]{2}$").WithMessage("Select your nationality.");
            passenger.RuleFor(p => p.DocumentType).IsInEnum();
            passenger.RuleFor(p => p.DocumentIssuingCountry).Matches("^[A-Z]{2}$").WithMessage("Select the issuing country.");
            // Both document types on the passenger page require an expiry date.
            passenger.RuleFor(p => p.DocumentExpiryDate).NotNull().WithMessage("Enter the document expiry date.");
            passenger.RuleFor(p => p.DocumentNumber).NotEmpty()
                .Must((p, number) => IsValidDocumentNumber(p.DocumentType, number))
                .WithMessage("Enter a valid document number.");
            passenger.RuleForEach(p => p.Seats).ChildRules(seat =>
            {
                seat.RuleFor(s => s.FlightId).GreaterThan(0);
                seat.RuleFor(s => s.FlightSeatId).GreaterThan(0);
            });
        });

        // One document number belongs to one traveller: the same number twice on a booking (infants included) is refused.
        RuleFor(x => x).Custom((request, context) =>
        {
            var firstSeen = new Dictionary<string, int>();
            for (var index = 0; index < request.Passengers.Count; index++)
            {
                var number = DocumentNumbers.Normalize(request.Passengers[index].DocumentNumber);
                if (number.Length == 0)
                {
                    continue;
                }

                if (firstSeen.TryGetValue(number, out var earlier))
                {
                    context.AddFailure($"Passengers[{index}].DocumentNumber",
                        $"Bu sənəd nömrəsi {earlier + 1}-ci sərnişinin nömrəsi ilə eynidir. Hər sərnişinin sənədi fərqli olmalıdır.");
                }
                else
                {
                    firstSeen[number] = index;
                }
            }
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
