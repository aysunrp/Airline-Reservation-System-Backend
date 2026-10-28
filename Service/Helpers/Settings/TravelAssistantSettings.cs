namespace Service.Helpers.Settings;

/// <summary>
/// The Ask AEROVA travel assistant. Everything here except <see cref="ApiKey"/> is safe to keep in appsettings.json; the key belongs in
/// user-secrets (development) or the environment (TravelAssistant__ApiKey) and is never sent to the browser or written to the log.
/// Without a key the API runs and the assistant answers 503 "not available".
/// </summary>
public class TravelAssistantSettings
{
    public const string SectionName = "TravelAssistant";

    /// <summary>Which provider answers. "Anthropic" is the one that is built in; the provider sits behind IAssistantProvider so another can be added.</summary>
    public string Provider { get; set; } = "Anthropic";

    /// <summary>The model that answers (a fast, inexpensive one is enough for travel questions).</summary>
    public string Model { get; set; } = "claude-haiku-5-5";

    /// <summary>The provider's chat endpoint.</summary>
    public string Endpoint { get; set; } = "https://api.anthropic.com/v1/messages";

    /// <summary>The provider's API version header.</summary>
    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>The provider's key. Secret: user-secrets or environment only.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Seconds to wait for the provider before the request is given up (504).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>The longest answer the provider may write, in tokens (this is also what bounds the cost of one question).</summary>
    public int MaxOutputTokens { get; set; } = 700;

    /// <summary>Longest single message, in characters.</summary>
    public int MaxMessageLength { get; set; } = 2000;

    /// <summary>Most messages (questions and answers together) accepted in one request.</summary>
    public int MaxMessages { get; set; } = 20;

    /// <summary>Most characters of all the messages of one request together.</summary>
    public int MaxTotalLength { get; set; } = 12000;

    /// <summary>Questions one IP address may ask per minute (429 beyond it).</summary>
    public int RateLimitPerMinute { get; set; } = 10;

    /// <summary>Minutes the facts read from the database (fare rules, baggage, meals) are kept before they are read again.</summary>
    public int FactsCacheMinutes { get; set; } = 10;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && string.Equals(Provider, "Anthropic", StringComparison.OrdinalIgnoreCase);
}
