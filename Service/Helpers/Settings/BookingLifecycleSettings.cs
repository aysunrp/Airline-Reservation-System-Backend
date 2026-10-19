namespace Service.Helpers.Settings;

/// <summary>Background follow-up of bookings: mock refund processing and completing trips that have flown.</summary>
public class BookingLifecycleSettings
{
    public const string SectionName = "BookingLifecycle";

    /// <summary>Turns the background job on or off.</summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>How often the job runs.</summary>
    public int CheckIntervalSeconds { get; set; } = 60;

    /// <summary>Minutes after the cancellation at which the mock provider starts processing a refund.</summary>
    public int RefundProcessingAfterMinutes { get; set; } = 5;

    /// <summary>Minutes after the cancellation at which the mock provider completes a refund.</summary>
    public int RefundCompletedAfterMinutes { get; set; } = 30;
}
