using System.Net;

namespace Service.Helpers.Mail;

/// <summary>
/// The e-mails of the newsletter subscription, in Azerbaijani, English and Russian. Every one of them carries a link to opt out. They are
/// not newsletters: the content of a newsletter is written by AEROVA when there is something to say.
/// </summary>
public static class NewsletterEmailTemplates
{
    public static (string Subject, string Text, string Html) Confirmation(string? language, string link, int hours, string unsubscribeLink)
    {
        var (subject, intro, button, validity, ignore, optOut) = EmailTemplates.NormalizeLanguage(language) switch
        {
            "az" => ("AEROVA abunəliyinizi təsdiqləyin",
                "AEROVA yeniliklərinə, istiqamət ilhamına və xüsusi təkliflərə abunə olmaq istədiyiniz üçün təşəkkür edirik. Abunəliyi aktivləşdirmək üçün e-poçt ünvanınızı təsdiqləyin:",
                "Abunəliyi təsdiqlə",
                $"Keçid {hours} saat etibarlıdır və yalnız bir dəfə işləyir.",
                "Bu sorğunu siz etməmisinizsə, bu məktuba məhəl qoymayın: təsdiq olmadan heç bir məktub göndərilməyəcək.",
                "Abunəlikdən çıx"),
            "ru" => ("Подтвердите подписку на AEROVA",
                "Спасибо, что хотите получать новости AEROVA, идеи для путешествий и специальные предложения. Чтобы активировать подписку, подтвердите адрес электронной почты:",
                "Подтвердить подписку",
                $"Ссылка действует {hours} ч. и работает один раз.",
                "Если вы не отправляли этот запрос, просто проигнорируйте письмо: без подтверждения рассылка не придёт.",
                "Отписаться"),
            _ => ("Confirm your AEROVA subscription",
                "Thank you for asking to receive AEROVA news, destination inspiration and special offers. Confirm your email address to activate the subscription:",
                "Confirm subscription",
                $"The link is valid for {hours} hours and works once.",
                "If you did not make this request, ignore this message: nothing will be sent without confirmation.",
                "Unsubscribe")
        };

        return Build(subject, intro, button, link, validity + "\n" + ignore, optOut, unsubscribeLink);
    }

    /// <summary>Sent when someone asks to subscribe an address that is already subscribed: it tells the owner, and shows how to opt out.</summary>
    public static (string Subject, string Text, string Html) AlreadySubscribed(string? language, string unsubscribeLink)
    {
        var (subject, intro, optOut) = EmailTemplates.NormalizeLanguage(language) switch
        {
            "az" => ("AEROVA abunəliyiniz artıq aktivdir",
                "Bu e-poçt ünvanı artıq AEROVA yenilikləri üçün abunədir, yenidən təsdiqləmək lazım deyil. Bu sorğunu siz etməmisinizsə, heç nə etmənizə ehtiyac yoxdur.",
                "Abunəlikdən çıx"),
            "ru" => ("Ваша подписка на AEROVA уже активна",
                "Этот адрес уже подписан на новости AEROVA, подтверждать повторно не нужно. Если вы не отправляли этот запрос, ничего делать не требуется.",
                "Отписаться"),
            _ => ("Your AEROVA subscription is already active",
                "This address is already subscribed to AEROVA news, so there is nothing to confirm again. If you did not make this request, you do not need to do anything.",
                "Unsubscribe")
        };

        return Build(subject, intro, null, null, string.Empty, optOut, unsubscribeLink);
    }

    private static (string Subject, string Text, string Html) Build(
        string subject, string intro, string? button, string? link, string notes, string optOut, string unsubscribeLink)
    {
        var text = intro + "\n" + (link is null ? string.Empty : link + "\n") + (notes.Length == 0 ? string.Empty : "\n" + notes + "\n") +
                   "\n" + optOut + ": " + unsubscribeLink + "\n\nAEROVA";

        string E(string value) => WebNetEncode(value);
        var html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:0 auto;padding:24px;color:#0b1a2c\">" +
            "<p style=\"letter-spacing:.2em;color:#9a7844;font-size:13px;margin:0 0 18px\">AEROVA</p>" +
            $"<p style=\"line-height:1.55\">{E(intro)}</p>" +
            (link is null ? string.Empty :
                $"<p style=\"margin:22px 0\"><a href=\"{E(link)}\" style=\"background:#c6a36a;color:#0b1a2c;text-decoration:none;padding:12px 22px;border-radius:999px;font-weight:bold;display:inline-block\">{E(button ?? subject)}</a></p>") +
            (notes.Length == 0 ? string.Empty : $"<p style=\"font-size:13px;color:#555;white-space:pre-line\">{E(notes)}</p>") +
            (link is null ? string.Empty : $"<p style=\"font-size:12px;color:#888;word-break:break-all\">{E(link)}</p>") +
            $"<hr style=\"border:none;border-top:1px solid #e5e0d6;margin:22px 0\">" +
            $"<p style=\"font-size:12px;color:#888\"><a href=\"{E(unsubscribeLink)}\" style=\"color:#888\">{E(optOut)}</a></p>" +
            "</div>";

        return (subject, text, html);
    }

    private static string WebNetEncode(string value) => WebUtility.HtmlEncode(value);
}
