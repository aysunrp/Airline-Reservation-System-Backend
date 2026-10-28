namespace Service.Helpers.Settings;

/// <summary>
/// The Ask AEROVA travel assistant. Everything here except <see cref="ApiKey"/> is safe to keep in appsettings.json; the key belongs in
/// user-secrets (development) or the environment (TravelAssistant__ApiKey) and is never sent to the browser or written to the log.
/// Without a key the API runs and the assistant answers 503 "not available".
/// </summary>
public class TravelAssistantSettings
{
    public const string SectionName = "TravelAssistant";

    /// <summary>Which provider answers. "Gemini" (Google AI Studio, the Gemini API) is the one that is built in; the provider sits behind IAssistantProvider so another can be added.</summary>
    public string Provider { get; set; } = "Gemini";

    /// <summary>The model that answers (a fast, inexpensive one is enough for travel questions). "check-assistant --models" lists the ones the key can use.</summary>
    public string Model { get; set; } = "gemini-3.5-flash";

    /// <summary>The provider's API address; the model and method are added to it.</summary>
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>
    /// How many tokens the model may spend thinking before it answers; 0 turns thinking off (right for short travel answers, and a model that
    /// thinks would otherwise use up the answer limit first). Leave it empty for a model that cannot turn thinking off.
    /// </summary>
    public int? ThinkingBudget { get; set; } = 0;

    /// <summary>The provider's key (a Google AI Studio key). Secret: user-secrets or environment only.</summary>
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

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && string.Equals(Provider, "Gemini", StringComparison.OrdinalIgnoreCase);

    /// <summary>The key as it is sent: a key pasted with a space or a new line around it still works.</summary>
    public string TrimmedApiKey => (ApiKey ?? string.Empty).Trim();

    /// <summary>
    /// False when the key cannot be a Gemini key: it has a character outside printable ASCII (a letter such as "Ç", a space, a quote), which is
    /// what happens when the placeholder text of an instruction is stored instead of the key, or it starts with "sk-" (a key of another provider,
    /// which must never be sent to Google).
    /// </summary>
    public bool HasWellFormedKey => TrimmedApiKey.Length > 0
                                    && TrimmedApiKey.All(c => c is > ' ' and <= '~' && c is not ('"' or '\''))
                                    && !TrimmedApiKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase);
}
