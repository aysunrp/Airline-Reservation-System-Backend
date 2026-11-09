namespace Service.Helpers;

/// <summary>The names of the audited actions, written once so the log and its filters agree.</summary>
public static class AuditActions
{
    public const string UserCreated = "UserCreated";
    public const string UserRoleChanged = "UserRoleChanged";
    public const string UserDeactivated = "UserDeactivated";
    public const string UserActivated = "UserActivated";
    public const string UserDeleted = "UserDeleted";
    public const string AccountDeletionRequested = "AccountDeletionRequested";
    public const string AccountDeletionRefused = "AccountDeletionRefused";
    public const string AccountDeleted = "AccountDeleted";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordCheckRefused = "PasswordCheckRefused";
    public const string EmailChanged = "EmailChanged";
    public const string SessionsRevoked = "SessionsRevoked";
    public const string FlightCreated = "FlightCreated";
    public const string FlightUpdated = "FlightUpdated";
    public const string FlightDeleted = "FlightDeleted";
    public const string SeatChanged = "SeatChanged";
    public const string ScheduleCreated = "ScheduleCreated";
    public const string ScheduleUpdated = "ScheduleUpdated";
    public const string ScheduleDeleted = "ScheduleDeleted";
    public const string FlightsGenerated = "FlightsGenerated";
    public const string BookingCancelledByAdmin = "BookingCancelledByAdmin";
    public const string RefundRetried = "RefundRetried";
    public const string AccessDenied = "AccessDenied";
    public const string SuperAdminBootstrapped = "SuperAdminBootstrapped";
    public const string SiteContentUpdated = "SiteContentUpdated";
    public const string SiteContentCreated = "SiteContentCreated";
    public const string SiteContentDeleted = "SiteContentDeleted";
    public const string HeroVideoCreated = "HeroVideoCreated";
    public const string HeroVideoUpdated = "HeroVideoUpdated";
    public const string HeroVideoActivated = "HeroVideoActivated";
    public const string HeroVideoDeleted = "HeroVideoDeleted";
    public const string DestinationImageUpdated = "DestinationImageUpdated";
    public const string DestinationImageRemoved = "DestinationImageRemoved";
    public const string DestinationCityDeleted = "DestinationCityDeleted";
    public const string DestinationCountryDeleted = "DestinationCountryDeleted";
    public const string FaqItemCreated = "FaqItemCreated";
    public const string FaqItemUpdated = "FaqItemUpdated";
    public const string FaqItemDeleted = "FaqItemDeleted";
    public const string HomeCardCreated = "HomeCardCreated";
    public const string HomeCardUpdated = "HomeCardUpdated";
    public const string HomeCardDeleted = "HomeCardDeleted";
    public const string HomeCardImageUpdated = "HomeCardImageUpdated";
    public const string HomeCardImageRemoved = "HomeCardImageRemoved";
    public const string MediaFileReplaced = "MediaFileReplaced";
    public const string MediaFileDeleted = "MediaFileDeleted";
    public const string SiteImageUpdated = "SiteImageUpdated";
    public const string SiteImageRemoved = "SiteImageRemoved";
}
