namespace Service.Helpers.Settings;

public class AuthSettings
{
    public const string SectionName = "Auth";

    /// <summary>
    /// PBKDF2-SHA256 iterations of new password hashes (OWASP recommends 600,000). The count is stored inside every hash,
    /// so a hash made with fewer iterations keeps working and is upgraded the next time its owner signs in.
    /// </summary>
    public int PasswordHashIterations { get; set; } = 600_000;

    /// <summary>Failed sign-ins for one e-mail address (known or not) before sign-in is paused.</summary>
    public int MaxFailedSignIns { get; set; } = 5;

    /// <summary>How long sign-in stays paused for that e-mail address.</summary>
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Sign-in and register requests one IP address may make per minute.</summary>
    public int RateLimitPerMinute { get; set; } = 10;

    /// <summary>Requests per minute and IP address for the anonymous look-ups that can be guessed at (promo codes, booking reference and last name).</summary>
    public int LookupRateLimitPerMinute { get; set; } = 60;

    /// <summary>
    /// How long the role and active flag of an account are remembered when a token is checked against the database. A role change
    /// or a deactivation made through the API takes effect at once; one made straight in the database within this many seconds.
    /// </summary>
    public int UserStateCacheSeconds { get; set; } = 30;

    /// <summary>Address of the website. The links in e-mails open its verify-email.html and reset-password.html pages.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5500";

    /// <summary>How long the link that confirms an e-mail address works.</summary>
    public int VerifyEmailHours { get; set; } = 24;

    /// <summary>How long a password reset link from an older e-mail still works.</summary>
    public int ResetPasswordMinutes { get; set; } = 60;

    /// <summary>How long the six-digit code of a password reset works.</summary>
    public int ResetCodeMinutes { get; set; } = 10;

    /// <summary>Wrong guesses allowed for one reset code; after that the code is dead and a new one has to be asked for.</summary>
    public int ResetCodeMaxAttempts { get; set; } = 5;

    /// <summary>Reset codes one account can be sent per hour, so the codes cannot be used for endless guessing or for mail bombing.</summary>
    public int ResetCodesPerHour { get; set; } = 5;

    /// <summary>How long the person has, after typing the right code, to choose the new password.</summary>
    public int ResetTicketMinutes { get; set; } = 10;

    /// <summary>Seconds before another e-mail of the same kind can be sent to the same account.</summary>
    public int EmailCooldownSeconds { get; set; } = 60;

    /// <summary>
    /// A signed-in account needs a verified e-mail address to book. Only enforced while the API can send e-mail at all:
    /// without SMTP nobody could ever verify, and that must not lock every customer out.
    /// </summary>
    public bool RequireVerifiedEmailForBooking { get; set; } = true;

    /// <summary>
    /// A flight can only be booked while signed in to an account. Guests cannot start a booking; bookings that guests made before
    /// this rule still open with their reference and a passenger's last name. Tests that book as a guest switch this off.
    /// </summary>
    public bool RequireSignInToBook { get; set; } = true;

    /// <summary>
    /// Every booking is confirmed by a link sent to its contact address before it can be paid. Only enforced while the API can send
    /// e-mail at all, and only for bookings of an account.
    /// </summary>
    public bool RequireBookingEmailConfirmation { get; set; } = true;
}
