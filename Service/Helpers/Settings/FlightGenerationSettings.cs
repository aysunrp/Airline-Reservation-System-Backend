namespace Service.Helpers.Settings;

public class FlightGenerationSettings
{
    public const string SectionName = "FlightGeneration";

    /// <summary>How many days ahead, counting today at the departure airport, flights are created from schedules.</summary>
    public int DaysAhead { get; set; } = 90;
}
