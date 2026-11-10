namespace Service.Helpers.Settings;

public class PaymentSettings
{
    public const string SectionName = "Payments";

    /// <summary>
    /// The old pretend payment (POST api/payments with card details, approved without any provider) for local demos and tests.
    /// It never works while Stripe is configured, and is off unless switched on (appsettings.Development.json does).
    /// </summary>
    public bool AllowMockPayments { get; set; }
}
