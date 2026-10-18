namespace Service.Helpers.Settings;

/// <summary>Demo data for development. It only ever runs in the Development environment and only when enabled.</summary>
public class SeedSettings
{
    public const string SectionName = "Seed";

    /// <summary>Seeds the demo airports, aircraft, routes, schedules and booking options when the API starts.</summary>
    public bool Enabled { get; set; }

    /// <summary>After seeding, creates the flights of all active schedules for FlightGeneration:DaysAhead days.</summary>
    public bool GenerateFlights { get; set; } = true;
}
