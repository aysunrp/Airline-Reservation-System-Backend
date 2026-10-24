namespace AirlineReservationSystem.Helpers;

public static class RateLimitPolicies
{
    /// <summary>Sign-in and register: Auth:RateLimitPerMinute requests per minute for one IP address.</summary>
    public const string Auth = "auth";
}
