namespace Service.Helpers.Settings;

public class FlightGenerationSettings
{
    public const string SectionName = "FlightGeneration";

    /// <summary>How many days ahead, counting today at the departure airport, flights are created from schedules.</summary>
    public int DaysAhead { get; set; } = 90;

    /// <summary>Turns the daily background generation on or off; the admin endpoints keep working either way.</summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>Time of day, in UTC, when the daily generation runs ("HH:mm").</summary>
    public TimeOnly DailyRunTimeUtc { get; set; } = new(2, 0);

    /// <summary>Also runs once right after the application starts, e.g. to fill the window after a deployment.</summary>
    public bool RunOnStartup { get; set; }
}
