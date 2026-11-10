using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>Looks every few seconds for notifications that still have to be e-mailed, so the mail follows the news within moments.</summary>
public class NotificationEmailWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BookingLifecycleSettings _settings;
    private readonly ILogger<NotificationEmailWorker> _logger;

    public NotificationEmailWorker(IServiceScopeFactory scopeFactory, IOptions<BookingLifecycleSettings> settings, ILogger<NotificationEmailWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>One run in its own scope; a failure is logged and the next run tries again.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<INotificationEmailService>().SendDueAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<IAdminAlertService>().SendDueAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Sending notification e-mails failed; it will be retried on the next run.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The same switch as the booking follow-up: tests and special setups turn the background jobs off together.
        if (!_settings.WorkerEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
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
