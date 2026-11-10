using System.Net;

namespace Service.Helpers.Mail;

/// <summary>The e-mail to the administrator: plain, English, with a link to the admin dashboard.</summary>
public static class AdminAlertEmailTemplate
{
    public static (string Text, string Html) Build(string subject, string body, string adminLink)
    {
        var text = $"{subject}\n\n{body}\n\n{adminLink}\n\nAEROVA admin notification";
        var html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;padding:24px;color:#0b1a2c\">" +
            "<p style=\"letter-spacing:.2em;color:#9a7844;font-size:13px;margin:0 0 18px\">AEROVA ADMIN</p>" +
            $"<h2 style=\"margin:0 0 12px;font-size:18px\">{WebUtility.HtmlEncode(subject)}</h2>" +
            $"<p style=\"white-space:pre-line;line-height:1.55\">{WebUtility.HtmlEncode(body)}</p>" +
            $"<p style=\"margin:22px 0\"><a href=\"{WebUtility.HtmlEncode(adminLink)}\" style=\"background:#c6a36a;color:#0b1a2c;text-decoration:none;padding:10px 20px;border-radius:999px;font-weight:bold\">Open admin</a></p>" +
            "</div>";
        return (text, html);
    }
}
