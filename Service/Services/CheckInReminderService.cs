using Domain.Enums;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Reminds travellers of a flight that is about to depart. Only bookings that belong to an account are reminded, because a notification
/// needs a user; guests keep their confirmation e-mail address on the booking but have no notification list.
/// </summary>
public class CheckInReminderService : ICheckInReminderService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationService _notificationService;
    private readonly BookingLifecycleSettings _settings;
    private readonly TimeProvider _timeProvider;

    public CheckInReminderService(
        IBookingRepository bookingRepository,
        INotificationRepository notificationRepository,
        INotificationService notificationService,
        IOptions<BookingLifecycleSettings> settings,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _notificationRepository = notificationRepository;
        _notificationService = notificationService;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<CheckInReminderResult> QueueDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var hours = Math.Max(1, _settings.CheckInReminderHours);
        var candidates = await _bookingRepository.GetConfirmedDepartingBetweenAsync(now, now.AddHours(hours), cancellationToken);
        if (candidates.Count == 0)
        {
            return new CheckInReminderResult(0);
        }

        var alreadyReminded = await _notificationRepository.GetBookingIdsWithTypeAsync(
            NotificationType.CheckInReminder, candidates.Select(b => b.Id).ToList(), cancellationToken);

        var reminders = 0;
        foreach (var booking in candidates)
        {
            if (alreadyReminded.Contains(booking.Id))
            {
                continue;
            }

            // The reminder is about the journey's first flight; a later leg that is close does not start the clock.
            var first = booking.Flights.OrderBy(f => f.LegOrder).First().Flight;
            if (first.DepartureAt <= now || first.DepartureAt > now.AddHours(hours))
            {
                continue;
            }

            await _notificationService.QueueAsync(
                booking.UserId!.Value,
                booking.Id,
                NotificationType.CheckInReminder,
                "Check-in Reminder",
                $"Your flight {first.FlightNumber} from {first.Route.Origin.City} to {first.Route.Destination.City} departs within {hours} hours. Booking reference {booking.Pnr}.",
                cancellationToken);
            reminders++;
        }

        if (reminders > 0)
        {
            await _notificationRepository.SaveChangesAsync(cancellationToken);
        }

        return new CheckInReminderResult(reminders);
    }
}
