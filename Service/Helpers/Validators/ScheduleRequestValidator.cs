using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Validators;

public class ScheduleRequestValidator : AbstractValidator<ScheduleRequestDto>
{
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
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("The end date cannot be before the start date.");
    }
}
