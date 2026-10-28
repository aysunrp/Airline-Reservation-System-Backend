namespace Service.Helpers.Settings;

/// <summary>
/// Why Ask AEROVA could not answer, as a short code. The code travels with the 503 (field "code") and is written in the server log, so
/// the failing step is known without guessing. It says what failed, never a secret, a question or the provider's own words.
/// </summary>
public static class AssistantErrorCodes
{
    /// <summary>No provider key (TravelAssistant:ApiKey) or an unsupported provider name: nothing was asked.</summary>
    public const string NotConfigured = "assistant_not_configured";

    /// <summary>The stored key cannot be an API key (non-ASCII letters, spaces or quotes inside it): a placeholder or a bad paste. Nothing was sent.</summary>
    public const string KeyMalformed = "assistant_key_malformed";

    /// <summary>The provider refused the key (401 or 403): wrong, revoked, or without API access.</summary>
    public const string KeyRejected = "assistant_key_rejected";

    /// <summary>The provider does not know the model or the endpoint (404): TravelAssistant:Model or :Endpoint is wrong, or the key has no access to the model.</summary>
    public const string ModelUnavailable = "assistant_model_unavailable";

    /// <summary>The account has no credit left or billing is not set up (the provider's own billing error).</summary>
    public const string Billing = "assistant_billing";

    /// <summary>The provider is overloaded or the account's rate limit is reached (429, 529).</summary>
    public const string Busy = "assistant_busy";

    /// <summary>The provider did not accept the request (other 4xx).</summary>
    public const string RequestRejected = "assistant_request_rejected";

    /// <summary>The provider failed on its side (5xx).</summary>
    public const string ProviderError = "assistant_provider_error";

    /// <summary>The provider could not be reached (DNS, firewall, no internet, wrong endpoint host).</summary>
    public const string Unreachable = "assistant_unreachable";

    /// <summary>The provider answered, but not with usable text.</summary>
    public const string BadAnswer = "assistant_bad_answer";

    /// <summary>The provider's safety rules stopped the answer (the question or the answer was blocked).</summary>
    public const string Blocked = "assistant_blocked";

    /// <summary>What an operator should check, in plain words (used by the check-assistant command; never shown to travellers).</summary>
    public static string Describe(string? code) => code switch
    {
        NotConfigured => "Ask AEROVA is not set up: TravelAssistant:ApiKey is empty (or TravelAssistant:Provider is not \"Gemini\"). Set the key with user-secrets or the TravelAssistant__ApiKey environment variable.",
        KeyMalformed => "TravelAssistant:ApiKey is not a real API key: it contains characters that cannot be part of one (letters such as \u00c7 or \u018f, spaces or quotes). It looks like the placeholder text of an instruction or a bad paste. It can also be the key of another provider (keys starting with \"sk-\" are never sent to Google). Replace it with the key from Google AI Studio (aistudio.google.com/apikey, it starts with \"AIza\") and restart the API.",
        KeyRejected => "The provider refused the key. Check that TravelAssistant:ApiKey is a current Gemini API key from Google AI Studio (not deleted or restricted), copied completely.",
        ModelUnavailable => "The provider does not know this model or endpoint. Run check-assistant --models to see the models this key can use, then set TravelAssistant:Model (and check TravelAssistant:Endpoint).",
        Billing => "The provider account has no credit or billing is not set up. Check the plan and billing of the Google AI Studio project.",
        Busy => "The provider is busy, or the free-tier rate limit or quota of the key's project is reached. Try again in a minute, or check the quota in Google AI Studio.",
        RequestRejected => "The provider rejected the request. Check TravelAssistant:Model, TravelAssistant:MaxOutputTokens and TravelAssistant:ThinkingBudget (a model that cannot turn thinking off needs it empty).",
        ProviderError => "The provider failed on its side. Try again later and check the provider's status page.",
        Unreachable => "The provider could not be reached. Check the internet connection, a firewall or antivirus, a proxy, and TravelAssistant:Endpoint.",
        BadAnswer => "The provider answered, but without usable text. Check TravelAssistant:Model, TravelAssistant:Endpoint and, for a model that thinks, TravelAssistant:ThinkingBudget.",
        Blocked => "The provider's safety rules stopped the answer. Try a different question; if every question is stopped, check the project's safety settings in Google AI Studio.",
        _ => "The assistant failed for a reason that is not known; see the API log."
    };
}
