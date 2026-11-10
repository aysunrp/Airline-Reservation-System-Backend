using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

public class BookingLifecycleWorkerTests
{
    private sealed class CountingRefunds : IRefundProcessingService
    {
        public int Calls;
        public Exception? Failure;

        public Task<RefundProcessingResult> ProcessDueAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Failure is null ? Task.FromResult(new RefundProcessingResult(1, 1)) : Task.FromException<RefundProcessingResult>(Failure);
        }
    }

    private sealed class CountingRefundSender : IRefundService
    {
        public int Calls;

        public Task SubmitAsync(int refundId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> SubmitDueAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(0);
        }

        public Task<string> ApplyProviderStateAsync(Domain.Entities.Refund refund, string? providerRefundId, string providerStatus, string? failureReason, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Service.Helpers.DTOs.Reservation.AdminRefundDto> RetryAsync(int refundId, AuditActor actor, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Service.Helpers.DTOs.Reservation.AdminRefundDto>> GetAllAsync(Domain.Enums.RefundStatus? status, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CountingCompletion : ITripCompletionService
    {
        public int Calls;

        public Task<TripCompletionResult> CompleteFinishedAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new TripCompletionResult(1, 2, 1));
        }
    }

    private sealed class CountingReminders : ICheckInReminderService
    {
        public int Calls;

        public Task<CheckInReminderResult> QueueDueAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new CheckInReminderResult(1));
        }
    }

    private readonly CountingRefunds _refunds = new();
    private readonly CountingCompletion _completion = new();
    private readonly CountingReminders _reminders = new();

    private BookingLifecycleWorker CreateWorker(BookingLifecycleSettings? settings = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRefundProcessingService>(_refunds);
        services.AddSingleton<IRefundService>(new CountingRefundSender());
        services.AddSingleton<ITripCompletionService>(_completion);
        services.AddSingleton<ICheckInReminderService>(_reminders);
        return new BookingLifecycleWorker(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings ?? new BookingLifecycleSettings()),
            NullLogger<BookingLifecycleWorker>.Instance);
    }

    [Fact]
    public void Defaults_MatchTheApprovedTimings()
    {
        var settings = new BookingLifecycleSettings();

        Assert.Equal((true, 60, 5, 30), (settings.WorkerEnabled, settings.CheckIntervalSeconds, settings.RefundProcessingAfterMinutes, settings.RefundCompletedAfterMinutes));
        Assert.Equal(24, settings.CheckInReminderHours);
    }

    [Fact]
    public async Task OneRun_ProcessesRefundsAndCompletesTrips()
    {
        await CreateWorker().RunOnceAsync(CancellationToken.None);

        Assert.Equal((1, 1, 1), (_refunds.Calls, _completion.Calls, _reminders.Calls));
    }

    [Fact]
    public async Task AFailure_IsCaught_AndTheNextRunWorks()
    {
        var worker = CreateWorker();
        _refunds.Failure = new TimeoutException("The database did not answer.");

        await worker.RunOnceAsync(CancellationToken.None);
        _refunds.Failure = null;
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, _refunds.Calls);
        Assert.Equal(1, _completion.Calls);
    }

    [Fact]
    public async Task StartedWorker_RunsRightAway_AndStopsCleanly()
    {
        var worker = CreateWorker();

        await worker.StartAsync(CancellationToken.None);
        for (var i = 0; i < 50 && _completion.Calls == 0; i++)
        {
            await Task.Delay(20);
        }

        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, _completion.Calls);
    }

    [Fact]
    public async Task DisabledWorker_DoesNothing()
    {
        var worker = CreateWorker(new BookingLifecycleSettings { WorkerEnabled = false });

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, _refunds.Calls);
    }
}
