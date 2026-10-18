using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Validators;

public class ScheduleRequestValidator : AbstractValidator<ScheduleRequestDto>
{
    private const int MaxStops = 3;
    private const int MaxLayoverMinutes = 24 * 60;

    // The arrival can be up to two days after departure, so no stop can be reached later than that.
    private const int MaxArrivalOffsetMinutes = 3 * 24 * 60;

    public ScheduleRequestValidator()
    {
        RuleFor(x => x.FlightNumber).NotEmpty()
            .Must(FlightNumber.IsValid).WithMessage("Use a flight number like AV 101.");
        RuleFor(x => x.RouteId).GreaterThan(0);
        RuleFor(x => x.DefaultAircraftId).GreaterThan(0).When(x => x.DefaultAircraftId is not null);
        RuleFor(x => x.ArrivalDayOffset).InclusiveBetween(0, 2);
        RuleFor(x => x.ArrivalTime).GreaterThan(x => x.DepartureTime).When(x => x.ArrivalDayOffset == 0)
            .WithMessage("Arrival must be after departure, or set the arrival to the next day.");
        RuleFor(x => x.DaysOfWeek)
            .Must(d => d != ScheduleDays.None && (d & ~ScheduleDays.Everyday) == 0)
            .WithMessage("Select at least one day of the week.");
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Enter the date the schedule starts.");
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("The end date cannot be before the start date.");

        RuleFor(x => x.Fares).NotEmpty().WithMessage("Enter the base price for at least one cabin.");
        RuleFor(x => x.Fares)
            .Must(f => f.Select(fare => fare.CabinClass).Distinct().Count() == f.Count)
            .When(x => HasNoEmptyEntries(x.Fares))
            .WithMessage("Each cabin can have only one price.");
        RuleForEach(x => x.Fares).NotNull().WithMessage("Enter the cabin and its price.").ChildRules(fare =>
        {
            fare.RuleFor(f => f.CabinClass).IsInEnum();
            fare.RuleFor(f => f.BasePrice).GreaterThan(0).LessThan(100_000);
        });

        RuleFor(x => x.Stops).NotNull().Must(s => s is null || s.Count <= MaxStops)
            .WithMessage($"A schedule can have at most {MaxStops} stops.");
        RuleForEach(x => x.Stops).NotNull().WithMessage("Enter the stop details.").ChildRules(stop =>
        {
            stop.RuleFor(s => s.SequenceNo).GreaterThan(0);
            stop.RuleFor(s => s.AirportCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter airport code.");
            stop.RuleFor(s => s.ArrivalOffsetMinutes).InclusiveBetween(0, MaxArrivalOffsetMinutes);
            stop.RuleFor(s => s.LayoverMinutes).InclusiveBetween(0, MaxLayoverMinutes);
        });
        // The rules below compare stops with each other, so they only run once every entry is present.
        When(x => HasNoEmptyEntries(x.Stops), () =>
        {
            RuleFor(x => x.Stops)
                .Must(s => s.Select(stop => stop.SequenceNo).Distinct().Count() == s.Count)
                .WithMessage("Each stop number can be used only once.");
            RuleFor(x => x.Stops)
                .Must(s => s.Select(stop => stop.SequenceNo).Order().SequenceEqual(Enumerable.Range(1, s.Count)))
                .When(x => x.Stops.Select(stop => stop.SequenceNo).Distinct().Count() == x.Stops.Count)
                .WithMessage("Number the stops 1, 2, 3 ... in flying order.");
            RuleFor(x => x.Stops)
                .Must(s => s.Select(stop => (stop.AirportCode ?? string.Empty).Trim().ToUpperInvariant()).Distinct().Count() == s.Count)
                .WithMessage("The same airport cannot be a stop twice.");
            RuleFor(x => x.Stops)
                .Must(ReachesEachStopAfterLeavingThePreviousOne)
                .WithMessage("Each stop must be reached after the aircraft leaves the previous stop.");
        });
    }

    private static bool HasNoEmptyEntries<T>(List<T>? items) where T : class
        => items is not null && items.All(item => item is not null);

    private static bool ReachesEachStopAfterLeavingThePreviousOne(List<ScheduleStopRequestDto> stops)
    {
        var ordered = stops.OrderBy(s => s.SequenceNo).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].ArrivalOffsetMinutes <= ordered[i - 1].ArrivalOffsetMinutes + ordered[i - 1].LayoverMinutes)
            {
                return false;
            }
        }

        return true;
    }
}
