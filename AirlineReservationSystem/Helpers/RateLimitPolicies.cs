namespace AirlineReservationSystem.Helpers;

public static class RateLimitPolicies
{
    /// <summary>Sign-in and register: Auth:RateLimitPerMinute requests per minute for one IP address.</summary>
    public const string Auth = "auth";

    /// <summary>Anonymous look-ups that can be guessed at (promo codes, booking reference and last name): Auth:LookupRateLimitPerMinute.</summary>
    public const string Lookup = "lookup";

    /// <summary>Questions to Ask AEROVA (each one costs money at the AI provider): TravelAssistant:RateLimitPerMinute per IP address.</summary>
    public const string Assistant = "assistant";

    /// <summary>Newsletter subscribe, confirm and unsubscribe: Newsletter:RateLimitPerMinute per IP address.</summary>
    public const string Newsletter = "newsletter";
}
