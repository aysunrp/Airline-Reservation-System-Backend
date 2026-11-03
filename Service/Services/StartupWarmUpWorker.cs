using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.Repositories.Interfaces;

namespace Service.Services;

/// <summary>
/// Runs the queries the first visitors hit (the airport list, "my trips") once, right after start-up, in the background. The first
/// call of each query builds the Entity Framework model and compiles its SQL, which can take several seconds; doing it here means no
/// customer waits for it. A failure is ignored: the queries are then simply compiled on their first real use, as before.
/// </summary>
public class StartupWarmUpWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StartupWarmUpWorker> _logger;

    public StartupWarmUpWorker(IServiceScopeFactory scopeFactory, ILogger<StartupWarmUpWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let the application start answering before the warm-up uses the database.
            await Task.Delay(TimeSpan.FromMilliseconds(50), stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IAirportRepository>().GetActiveAsync(stoppingToken);
            await scope.ServiceProvider.GetRequiredService<IBookingRepository>().GetSummariesByUserIdAsync(0, stoppingToken);
            _logger.LogInformation("Start-up warm-up finished.");
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }
        catch (Exception exception)
        {
            _logger.LogInformation("Start-up warm-up skipped ({ExceptionType}).", exception.GetType().Name);
        }
    }
}
