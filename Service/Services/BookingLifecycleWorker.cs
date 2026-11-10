using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>Periodically processes mock refunds, completes trips that have flown and sends check-in reminders.</summary>
public class BookingLifecycleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BookingLifecycleSettings _settings;
    private readonly ILogger<BookingLifecycleWorker> _logger;

    public BookingLifecycleWorker(IServiceScopeFactory scopeFactory, IOptions<BookingLifecycleSettings> settings, ILogger<BookingLifecycleWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>One run in its own scope; failures are logged and retried on the next run.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var refunds = await scope.ServiceProvider.GetRequiredService<IRefundProcessingService>().ProcessDueAsync(cancellationToken);
            var sent = await scope.ServiceProvider.GetRequiredService<IRefundService>().SubmitDueAsync(cancellationToken);
            var trips = await scope.ServiceProvider.GetRequiredService<ITripCompletionService>().CompleteFinishedAsync(cancellationToken);
            var reminders = await scope.ServiceProvider.GetRequiredService<ICheckInReminderService>().QueueDueAsync(cancellationToken);

            if (sent + refunds.Processing + refunds.Completed + trips.Flights + trips.Tickets + trips.Bookings + reminders.Reminders > 0)
            {
                _logger.LogInformation(
                    "Refunds: {Sent} sent to Stripe, {Processing} processing, {Completed} completed. Completed {Flights} flights, {Tickets} tickets and {Bookings} bookings. Sent {Reminders} check-in reminders.",
                    sent, refunds.Processing, refunds.Completed, trips.Flights, trips.Tickets, trips.Bookings, reminders.Reminders);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Booking follow-up failed; it will be retried on the next run.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.WorkerEnabled)
        {
            _logger.LogInformation("Booking follow-up is disabled (BookingLifecycle:WorkerEnabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _settings.CheckIntervalSeconds)));
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application is shutting down.
        }
    }
}
