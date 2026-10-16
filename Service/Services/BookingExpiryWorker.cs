using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>Periodically frees seats held by unpaid bookings whose hold has run out.</summary>
public class BookingExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BookingSettings _settings;
    private readonly ILogger<BookingExpiryWorker> _logger;

    public BookingExpiryWorker(IServiceScopeFactory scopeFactory, IOptions<BookingSettings> settings, ILogger<BookingExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _settings.ExpiryCheckIntervalSeconds)));

        try
        {
            do
            {
                try
                {
                    // Repositories and DbContext are scoped, so each run gets its own scope.
                    using var scope = _scopeFactory.CreateScope();
                    var expiryService = scope.ServiceProvider.GetRequiredService<IBookingExpiryService>();
                    var released = await expiryService.ReleaseExpiredAsync(stoppingToken);
                    if (released > 0)
                    {
                        _logger.LogInformation("Expired {Count} unpaid booking(s) and released their seats.", released);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Releasing expired bookings failed; it will be retried on the next run.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down.
        }
    }
}
