using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Flight;
using Service.Helpers.Settings;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

public class FlightGenerationWorkerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private static FlightGenerationResultDto Result(int created = 0)
        => new(Enumerable.Range(1, created).Select(i => new GeneratedFlightDto(i, 1, "AV 101", new DateOnly(2026, 11, i), default, default)).ToList(),
            0,
            new[] { new FlightGenerationSkipDto(2, "AV 202", null, "The schedule has no aircraft assigned.") },
            Array.Empty<FlightGenerationConflictDto>(),
            Array.Empty<string>());

    /// <summary>A generation service whose behaviour each test decides; counts how many scoped instances were made.</summary>
    private sealed class ScriptedGeneration : IFlightGenerationService
    {
        public static int Instances;
        public static Func<CancellationToken, Task<FlightGenerationResultDto>> Behaviour = _ => Task.FromResult(Result());

        public ScriptedGeneration() => Interlocked.Increment(ref Instances);

        public Task<FlightGenerationResultDto> GenerateAsync(int? scheduleId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
            => Behaviour(cancellationToken);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }

    private readonly ListLogger<FlightGenerationWorker> _logger = new();

    // Tests in this class share the static script, so xUnit runs them one after another (same class).
    private FlightGenerationWorker CreateWorker(FlightGenerationSettings? settings = null, Action<IServiceCollection>? register = null)
    {
        ScriptedGeneration.Instances = 0;
        var services = new ServiceCollection();
        if (register is null)
        {
            services.AddScoped<IFlightGenerationService, ScriptedGeneration>();
        }
        else
        {
            register(services);
        }

        return new FlightGenerationWorker(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings ?? new FlightGenerationSettings()),
            TimeProvider.System,
            _logger);
    }

    [Theory]
    [InlineData("2026-11-01T01:59:00", "2026-11-01T02:00:00")]
    [InlineData("2026-11-01T02:00:00", "2026-11-02T02:00:00")]
    [InlineData("2026-11-01T23:30:00", "2026-11-02T02:00:00")]
    public void NextRun_IsTheNextDailyRunTime(string now, string expected)
    {
        var next = FlightGenerationWorker.NextRunUtc(DateTime.SpecifyKind(DateTime.Parse(now), DateTimeKind.Utc), new TimeOnly(2, 0));

        Assert.Equal(DateTime.SpecifyKind(DateTime.Parse(expected), DateTimeKind.Utc), next);
    }

    [Fact]
    public void Defaults_RunDailyAtTwoUtcForNinetyDays()
    {
        var settings = new FlightGenerationSettings();

        Assert.Equal((90, true, new TimeOnly(2, 0), false), (settings.DaysAhead, settings.WorkerEnabled, settings.DailyRunTimeUtc, settings.RunOnStartup));
    }

    [Fact]
    public async Task EachRun_UsesAFreshScope_AndLogsTheSummary()
    {
        ScriptedGeneration.Behaviour = _ => Task.FromResult(Result(created: 3));
        var worker = CreateWorker();

        Assert.True(await worker.RunOnceAsync(CancellationToken.None));
        Assert.True(await worker.RunOnceAsync(CancellationToken.None));

        Assert.Equal(2, ScriptedGeneration.Instances);
        Assert.Contains(_logger.Entries, e => e.Message.Contains("3 created") && e.Message.Contains("1 skipped"));
        Assert.Contains(_logger.Entries, e => e.Message.Contains("AV 202") && e.Message.Contains("no aircraft"));
    }

    [Fact]
    public async Task AFailedRun_IsLogged_AndTheNextRunStillWorks()
    {
        var calls = 0;
        ScriptedGeneration.Behaviour = _ => ++calls == 1
            ? throw new TimeoutException("The database did not answer.")
            : Task.FromResult(Result(created: 1));
        var worker = CreateWorker();

        Assert.False(await worker.RunOnceAsync(CancellationToken.None));
        Assert.True(await worker.RunOnceAsync(CancellationToken.None));

        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("will run again"));
    }

    [Fact]
    public async Task ARunThatIsStillBusy_IsNotStartedTwice()
    {
        var release = new TaskCompletionSource<FlightGenerationResultDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        ScriptedGeneration.Behaviour = _ => release.Task;
        var worker = CreateWorker();

        var first = worker.RunOnceAsync(CancellationToken.None);
        Assert.False(await worker.RunOnceAsync(CancellationToken.None));
        release.SetResult(Result(created: 1));

        Assert.True(await first.WaitAsync(Wait));
        Assert.Equal(1, ScriptedGeneration.Instances);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("still running"));
    }

    [Fact]
    public async Task Cancellation_IsPassedOn()
    {
        ScriptedGeneration.Behaviour = async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Result();
        };
        var worker = CreateWorker();
        using var cancel = new CancellationTokenSource();

        var run = worker.RunOnceAsync(cancel.Token);
        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Wait));
        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Worker_RunsOnStartup_WhenAskedTo_AndStopsCleanly()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ScriptedGeneration.Behaviour = _ =>
        {
            started.TrySetResult();
            return Task.FromResult(Result());
        };
        // The daily time is set an hour in the past, so the next run is almost a day away whenever the test runs.
        var worker = CreateWorker(new FlightGenerationSettings { RunOnStartup = true, DailyRunTimeUtc = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-1)) });

        await worker.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None).WaitAsync(Wait);

        // After the startup run it waits for the daily time; stopping cancels that wait without another run.
        Assert.Equal(1, ScriptedGeneration.Instances);
        Assert.Contains(_logger.Entries, e => e.Message.StartsWith("Next flight generation run at"));
    }

    [Fact]
    public async Task DisabledWorker_NeverGenerates()
    {
        ScriptedGeneration.Behaviour = _ => throw new InvalidOperationException("Should not run.");
        var worker = CreateWorker(new FlightGenerationSettings { WorkerEnabled = false, RunOnStartup = true });

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None).WaitAsync(Wait);

        Assert.Equal(0, ScriptedGeneration.Instances);
        Assert.Contains(_logger.Entries, e => e.Message.Contains("disabled"));
    }

    [Fact]
    public async Task RepeatedRuns_WithTheRealService_DoNotDuplicateFlights()
    {
        var data = new TestData();
        var route = new Route { Id = 10, Origin = data.Baku, OriginAirportId = data.Baku.Id, Destination = data.London, DestinationAirportId = data.London.Id };
        var aircraft = new Aircraft { Id = 1, Name = "AEROVA Horizon", Model = "A350-900", RegistrationNumber = "4K-AV08" };
        aircraft.Seats.Add(new AircraftSeat { Id = 1, AircraftId = 1, RowNumber = 11, Letter = "A", CabinClass = CabinClass.Economy });
        var schedule = new FlightSchedule
        {
            Id = 1, FlightNumber = "AV 101", Route = route, RouteId = route.Id, DefaultAircraftId = aircraft.Id,
            DepartureTime = new TimeOnly(9, 30), ArrivalTime = new TimeOnly(13, 10),
            DaysOfWeek = ScheduleDays.Everyday, ValidFrom = new DateOnly(2026, 11, 1)
        };
        schedule.Fares.Add(new FlightScheduleFare { CabinClass = CabinClass.Economy, BasePrice = 420 });

        var flights = new FakeFlightRepository();
        var worker = CreateWorker(register: services =>
        {
            services.AddSingleton<IFlightRepository>(flights);
            services.AddSingleton<IFlightScheduleRepository>(new FakeFlightScheduleRepository(schedule));
            services.AddSingleton<IAircraftRepository>(new FakeAircraftRepository(aircraft));
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
            services.AddSingleton(Options.Create(new FlightGenerationSettings { DaysAhead = 7 }));
            services.AddScoped<IFlightGenerationService, FlightGenerationService>();
        });

        Assert.True(await worker.RunOnceAsync(CancellationToken.None));
        var afterFirstRun = (await flights.GetAllWithDetailsAsync()).Count;
        Assert.True(await worker.RunOnceAsync(CancellationToken.None));

        Assert.Equal(7, afterFirstRun);
        Assert.Equal(7, (await flights.GetAllWithDetailsAsync()).Count);
        Assert.Contains(_logger.Entries, e => e.Message.Contains("0 created, 7 already existing"));
    }
}
