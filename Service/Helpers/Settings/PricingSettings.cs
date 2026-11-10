namespace Service.Helpers.Settings;

public class PricingSettings
{
    public const string SectionName = "Pricing";

    /// <summary>Tax applied to the base fare (0.10 = 10%).</summary>
    public decimal TaxRate { get; set; } = 0.10m;

    /// <summary>Passengers younger than this age (in years) on the travel date are infants.</summary>
    public int InfantMaxAgeYears { get; set; } = 2;

    /// <summary>Passengers younger than this age (in years) on the travel date are children.</summary>
    public int ChildMaxAgeYears { get; set; } = 12;

    /// <summary>Share of the cabin fare an infant pays (0.10 = 10%).</summary>
    public decimal InfantFareRate { get; set; } = 0.10m;

    /// <summary>Share of the cabin fare a child pays (1.00 = full fare).</summary>
    public decimal ChildFareRate { get; set; } = 1.00m;
}
