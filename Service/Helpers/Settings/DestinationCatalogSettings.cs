namespace Service.Helpers.Settings;

public class DestinationCatalogSettings
{
    public const string SectionName = "DestinationCatalog";

    /// <summary>Adds the destination catalog when the API starts (once the migrations are applied). Safe to leave on: it only adds what is missing.</summary>
    public bool SeedOnStartup { get; set; } = true;
}
