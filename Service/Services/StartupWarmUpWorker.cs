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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => RunAsync(_scopeFactory, _logger, stoppingToken, TimeSpan.FromMilliseconds(50));

    /// <summary>
    /// The warm-up itself. Program.cs awaits it before the API starts listening, so the first customer never waits for the model and the
    /// queries to be built (the airport list and a booking looked up by its reference took about ten seconds on the first request).
    /// </summary>
    public static async Task RunAsync(IServiceScopeFactory scopeFactory, ILogger logger, CancellationToken cancellationToken, TimeSpan? delay = null)
    {
        try
        {
            if (delay is not null)
            {
                // Let the application start answering before the warm-up uses the database.
                await Task.Delay(delay.Value, cancellationToken);
            }

            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IAirportRepository>().GetActiveAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<IAirportRepository>().GetAirportIdsWithActiveRoutesAsync(cancellationToken);

            var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            await bookings.GetSummariesByUserIdAsync(0, cancellationToken);
            // The query behind "my booking", "manage booking" and the booking details page (a reference nobody has).
            await bookings.GetByPnrAsync("WARMUP", cancellationToken);
            logger.LogInformation("Start-up warm-up finished.");
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }
        catch (Exception exception)
        {
            logger.LogInformation("Start-up warm-up skipped ({ExceptionType}).", exception.GetType().Name);
        }
    }
}
