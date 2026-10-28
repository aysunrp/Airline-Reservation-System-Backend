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

    /// <summary>What an operator should check, in plain words (used by the check-assistant command; never shown to travellers).</summary>
    public static string Describe(string? code) => code switch
    {
        NotConfigured => "Ask AEROVA is not set up: TravelAssistant:ApiKey is empty (or TravelAssistant:Provider is not \"Anthropic\"). Set the key with user-secrets or the TravelAssistant__ApiKey environment variable.",
        KeyMalformed => "TravelAssistant:ApiKey is not a real API key: it contains characters that cannot be part of one (letters such as \u00c7 or \u018f, spaces or quotes). It looks like the placeholder text of an instruction or a bad paste. Replace it with the key copied from the provider's console (it starts with \"sk-\") and restart the API.",
        KeyRejected => "The provider refused the key. Check that TravelAssistant:ApiKey is a current API key (not expired or revoked), copied completely, and created for an account with API access.",
        ModelUnavailable => "The provider does not know this model or endpoint. Check TravelAssistant:Model and TravelAssistant:Endpoint, and that the key's account may use that model.",
        Billing => "The provider account has no credit or billing is not set up. Add credit or a payment method in the provider's console.",
        Busy => "The provider is busy or the account's rate limit is reached. Try again in a minute; check the limits in the provider's console.",
        RequestRejected => "The provider rejected the request. Check TravelAssistant:Model, TravelAssistant:MaxOutputTokens and TravelAssistant:ApiVersion.",
        ProviderError => "The provider failed on its side. Try again later and check the provider's status page.",
        Unreachable => "The provider could not be reached. Check the internet connection, a firewall or antivirus, a proxy, and TravelAssistant:Endpoint.",
        BadAnswer => "The provider answered, but without usable text. Check TravelAssistant:Model and TravelAssistant:Endpoint.",
        _ => "The assistant failed for a reason that is not known; see the API log."
    };
}
