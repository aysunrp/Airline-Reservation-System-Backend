namespace Service.Helpers.Settings;

/// <summary>
/// E-mail sending (Gmail SMTP: smtp.gmail.com, port 587, StartTls, and a Google "App password" as the password).
/// Everything except host, port and security belongs in user-secrets or environment variables (Smtp__UserName, Smtp__Password,
/// Smtp__SenderEmail), never in appsettings.json. Without a complete configuration the API runs and simply sends no e-mail.
/// </summary>
public class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    /// <summary>None, Auto, SslOnConnect, StartTls or StartTlsWhenAvailable (MailKit SecureSocketOptions). StartTls is the right one for port 587.</summary>
    public string Security { get; set; } = "StartTls";

    /// <summary>The Gmail address the mail is sent from (also the SMTP login).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>The 16-character Google App password. A secret: never logged, never in source control.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>The "From" address; for Gmail it must be the same account as <see cref="UserName"/>.</summary>
    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "AEROVA";

    public int TimeoutSeconds { get; set; } = 20;

    public bool IsComplete
        => !string.IsNullOrWhiteSpace(Host) && Port > 0
           && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(SenderEmail);
}
