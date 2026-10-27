namespace AirlineReservationSystem.Helpers;

public static class RateLimitPolicies
{
    /// <summary>Sign-in and register: Auth:RateLimitPerMinute requests per minute for one IP address.</summary>
    public const string Auth = "auth";

    /// <summary>Anonymous look-ups that can be guessed at (promo codes, booking reference and last name): Auth:LookupRateLimitPerMinute.</summary>
    public const string Lookup = "lookup";
}
