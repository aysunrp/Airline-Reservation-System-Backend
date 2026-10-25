namespace Service.Helpers;

/// <summary>The names of the audited actions, written once so the log and its filters agree.</summary>
public static class AuditActions
{
    public const string UserCreated = "UserCreated";
    public const string UserRoleChanged = "UserRoleChanged";
    public const string UserDeactivated = "UserDeactivated";
    public const string UserActivated = "UserActivated";
    public const string UserDeleted = "UserDeleted";
    public const string FlightCreated = "FlightCreated";
    public const string FlightUpdated = "FlightUpdated";
    public const string FlightDeleted = "FlightDeleted";
    public const string SeatChanged = "SeatChanged";
    public const string ScheduleCreated = "ScheduleCreated";
    public const string ScheduleUpdated = "ScheduleUpdated";
    public const string ScheduleDeleted = "ScheduleDeleted";
    public const string FlightsGenerated = "FlightsGenerated";
    public const string BookingCancelledByAdmin = "BookingCancelledByAdmin";
    public const string AccessDenied = "AccessDenied";
    public const string SuperAdminBootstrapped = "SuperAdminBootstrapped";
}
