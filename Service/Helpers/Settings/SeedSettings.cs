namespace Service.Helpers.Settings;

/// <summary>Demo data for development. It only ever runs in the Development environment and only when enabled.</summary>
public class SeedSettings
{
    public const string SectionName = "Seed";

    /// <summary>Seeds the demo airports, aircraft, routes, schedules and booking options when the API starts.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// With <see cref="Enabled"/>, also adds the demonstration network (the 16-country catalog, extra aircraft, routes and the schedules of
    /// November 2026 to January 2027). Off by default: it adds a few thousand flights.
    /// </summary>
    public bool Network { get; set; }

    /// <summary>
    /// Adds ONLY the regional network (direct flights between other countries, see RegionalNetworkData) when the API starts in Development:
    /// its aircraft, routes, schedules and the flights of those schedules. It does not need <see cref="Enabled"/> and does not touch the
    /// catalog or the other demo data, so it cannot bring back anything that was deleted. Off by default; safe to run again.
    /// </summary>
    public bool RegionalNetwork { get; set; }

    /// <summary>After seeding, creates the flights of all active schedules for FlightGeneration:DaysAhead days.</summary>
    public bool GenerateFlights { get; set; } = true;

    /// <summary>
    /// Creates a development Admin account from <see cref="AdminEmail"/> and <see cref="AdminPassword"/> when the API
    /// starts (Development only). Both values belong in user-secrets or environment variables, never in appsettings.
    /// </summary>
    public bool CreateAdmin { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}
