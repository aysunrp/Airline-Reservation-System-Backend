using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>Sends confirmed e-mail campaigns a batch at a time, so no web request waits for the mail server.</summary>
public class EmailCampaignWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BookingLifecycleSettings _settings;
    private readonly ILogger<EmailCampaignWorker> _logger;
    private bool _warnedAboutSetup;

    public EmailCampaignWorker(IServiceScopeFactory scopeFactory, IOptions<BookingLifecycleSettings> settings, ILogger<EmailCampaignWorker> logger)
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
            await scope.ServiceProvider.GetRequiredService<ICampaignService>().ProcessDueAsync(cancellationToken);
        }
        catch (Exception ex) when (CampaignService.IsMissingTable(ex))
        {
            // The migration was not applied yet: say so once instead of an error every few seconds.
            if (!_warnedAboutSetup)
            {
                _warnedAboutSetup = true;
                _logger.LogWarning("E-mail campaigns are paused: the campaign tables are missing. Apply the migration AddEmailCampaignsAndAdminAlerts.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Sending e-mail campaigns failed; it will be retried on the next run.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The same switch as the other background jobs: tests and special setups turn them off together.
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
