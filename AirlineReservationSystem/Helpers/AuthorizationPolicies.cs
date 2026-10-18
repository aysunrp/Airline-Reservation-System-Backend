namespace AirlineReservationSystem.Helpers;

public static class AuthorizationPolicies
{
    /// <summary>Admin panel endpoints: the token must carry the Admin role.</summary>
    public const string Admin = "Admin";
}
