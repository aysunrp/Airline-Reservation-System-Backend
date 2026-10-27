namespace AirlineReservationSystem.Helpers;

public static class AuthorizationPolicies
{
    /// <summary>Admin panel endpoints: the token must carry the Admin or the SuperAdmin role.</summary>
    public const string Admin = "Admin";

    /// <summary>Account and role management and the destructive admin actions: the token must carry the SuperAdmin role.</summary>
    public const string SuperAdmin = "SuperAdmin";
}
