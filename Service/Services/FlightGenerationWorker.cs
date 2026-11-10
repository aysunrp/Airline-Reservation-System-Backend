using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Once a day (at <see cref="FlightGenerationSettings.DailyRunTimeUtc"/>) creates the missing flights of all active
/// schedules for today and the configured days ahead. Generation is idempotent, so a run that repeats work, or that
/// overlaps with an admin request, only reports the flights as already existing.
/// </summary>
public class FlightGenerationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FlightGenerationSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FlightGenerationWorker> _logger;
    private readonly SemaphoreSlim _running = new(1, 1);

    public FlightGenerationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FlightGenerationSettings> settings,
        TimeProvider timeProvider,
        ILogger<FlightGenerationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>The next moment, strictly after <paramref name="nowUtc"/>, at which the daily run is due.</summary>
    public static DateTime NextRunUtc(DateTime nowUtc, TimeOnly runTimeUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc).ToDateTime(runTimeUtc, DateTimeKind.Utc);
        return today > nowUtc ? today : today.AddDays(1);
    }

    /// <summary>
    /// One generation run in its own scope. Returns false when it failed or was skipped because a run is already
    /// in progress; errors are logged and never stop later runs. Cancellation is passed on.
    /// </summary>
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _running.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Flight generation is still running from the previous trigger; this run is skipped.");
            return false;
        }

        try
        {
            _logger.LogInformation("Flight generation started for the next {DaysAhead} days.", _settings.DaysAhead);

            // Repositories and DbContext are scoped, so each run gets its own scope.
            using var scope = _scopeFactory.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IFlightGenerationService>().GenerateAsync(cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Flight generation finished: {Created} created, {Existing} already existing, {Skipped} skipped, {Conflicts} conflicts.",
                result.Created.Count, result.AlreadyExisting, result.Skipped.Count, result.Conflicts.Count);
            foreach (var skipped in result.Skipped)
            {
                _logger.LogInformation("Schedule {ScheduleId} ({FlightNumber}) skipped{Date}: {Reason}",
                    skipped.ScheduleId, skipped.FlightNumber, skipped.ScheduledDate is { } date ? $" on {date:yyyy-MM-dd}" : string.Empty, skipped.Reason);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Flight generation failed; it will run again at the next scheduled time.");
            return false;
        }
        finally
        {
            _running.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.WorkerEnabled)
        {
            _logger.LogInformation("Daily flight generation is disabled (FlightGeneration:WorkerEnabled = false).");
            return;
        }

        try
        {
            if (_settings.RunOnStartup)
            {
                await RunOnceAsync(stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var next = NextRunUtc(now, _settings.DailyRunTimeUtc);
                _logger.LogInformation("Next flight generation run at {NextRun:yyyy-MM-dd HH:mm} UTC.", next);

                await Task.Delay(next - now, _timeProvider, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application is shutting down.
        }
    }

    public override void Dispose()
    {
        _running.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
