using System.Net;

namespace Service.Helpers.Mail;

/// <summary>The account e-mails. The site speaks Azerbaijani, so that is the language of every e-mail unless a caller names another one. The name and the link are the only parts that change.</summary>
public static class EmailTemplates
{
    public static readonly string[] Languages = { "en", "az", "ru" };

    public static string NormalizeLanguage(string? language)
    {
        var value = (language ?? string.Empty).Trim().ToLowerInvariant();
        return Languages.Contains(value) ? value : "az";
    }

    /// <summary>Asks the person to confirm the address by opening the link.</summary>
    public static (string Subject, string Text, string Html) Verification(string? language, string name, string link, int hours)
    {
        var (subject, intro, validity, ignore) = NormalizeLanguage(language) switch
        {
            "az" => ("AEROVA e-poçt ünvanınızı təsdiqləyin",
                $"Salam {name},\n\nAEROVA-da qeydiyyatdan keçdiyiniz üçün təşəkkür edirik. E-poçt ünvanınızı təsdiqləmək üçün bu keçidi açın:",
                $"Keçid {hours} saat etibarlıdır və yalnız bir dəfə işləyir.",
                "Hesabı siz yaratmamısınızsa, bu məktuba məhəl qoymayın."),
            "ru" => ("Подтвердите адрес электронной почты AEROVA",
                $"Здравствуйте, {name}!\n\nСпасибо за регистрацию в AEROVA. Подтвердите адрес электронной почты, открыв ссылку:",
                $"Ссылка действует {hours} ч. и работает один раз.",
                "Если вы не создавали аккаунт, просто проигнорируйте это письмо."),
            _ => ("Confirm your AEROVA email address",
                $"Hello {name},\n\nThank you for registering with AEROVA. Confirm your email address by opening this link:",
                $"The link is valid for {hours} hours and works once.",
                "If you did not create an account, ignore this message.")
        };

        return Build(subject, intro, link, validity, ignore, name);
    }

    /// <summary>Asks the person to confirm one booking by opening the link; the seats are held until the time named.</summary>
    public static (string Subject, string Text, string Html) BookingConfirmation(string? language, string name, string pnr, string link, int minutes)
    {
        var (subject, intro, validity, ignore) = NormalizeLanguage(language) switch
        {
            "az" => ($"AEROVA rezervasiyanızı təsdiqləyin ({pnr})",
                $"Salam {name},\n\nYerləriniz ödəniş üçün tutulub (rezervasiya {pnr}). Ödənişə keçmək üçün rezervasiyanı bu keçidlə təsdiqləyin:",
                $"Keçid {minutes} dəqiqə etibarlıdır və yalnız bir dəfə işləyir. Bu vaxt keçəndə yerlər buraxılır.",
                "Bu rezervasiyanı siz etməmisinizsə, məktuba məhəl qoymayın."),
            "ru" => ($"Подтвердите бронирование AEROVA ({pnr})",
                $"Здравствуйте, {name}!\n\nМеста удержаны для оплаты (бронирование {pnr}). Чтобы перейти к оплате, подтвердите бронирование по ссылке:",
                $"Ссылка действует {minutes} мин. и работает один раз. По истечении времени места освобождаются.",
                "Если вы не делали это бронирование, просто проигнорируйте письмо."),
            _ => ($"Confirm your AEROVA booking ({pnr})",
                $"Hello {name},\n\nYour seats are held for payment (booking {pnr}). To go on to payment, confirm the booking with this link:",
                $"The link is valid for {minutes} minutes and works once. When the time is up the seats are released.",
                "If you did not make this booking, ignore this message.")
        };

        return Build(subject, intro, link, validity, ignore, name);
    }

    /// <summary>
    /// Asks the person to confirm the cancellation of a paid booking. The amounts are the estimate of the fare rules at the time of the
    /// request; the booking stays as it is until the link is opened and confirmed.
    /// </summary>
    public static (string Subject, string Text, string Html) CancelConfirmation(string? language, string name, string pnr, string flights,
        decimal refund, decimal fee, string currency, bool refundDue, string link, int minutes)
    {
        string Money(decimal amount) => amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " " + currency.ToUpperInvariant();
        var (subject, intro, estimate, none, validity, ignore) = NormalizeLanguage(language) switch
        {
            "az" => ($"AEROVA rezervasiyasının ləğvini təsdiqləyin ({pnr})",
                $"Salam {name},\n\n{pnr} rezervasiyasının ({flights}) ləğvi üçün sorğu aldıq. Rezervasiya yalnız bu keçidi açıb ləğvi təsdiqlədikdən sonra ləğv olunacaq:",
                $"Tarif qaydalarına görə təxmini qaytarılacaq məbləğ: {Money(refund)}" + (fee > 0 ? $" (ləğv cəriməsi {Money(fee)})." : "."),
                "Tarif qaydalarına görə bu rezervasiya üçün qaytarılacaq məbləğ yoxdur.",
                $"Keçid {minutes} dəqiqə etibarlıdır və yalnız bir dəfə işləyir.",
                "Bu sorğunu siz göndərməmisinizsə, məktuba məhəl qoymayın: rezervasiyanız qüvvədə qalır."),
            "ru" => ($"Подтвердите отмену бронирования AEROVA ({pnr})",
                $"Здравствуйте, {name}!\n\nМы получили запрос на отмену бронирования {pnr} ({flights}). Бронирование будет отменено только после того, как вы откроете ссылку и подтвердите отмену:",
                $"Ориентировочная сумма возврата по правилам тарифа: {Money(refund)}" + (fee > 0 ? $" (сбор за отмену {Money(fee)})." : "."),
                "По правилам тарифа возврат по этому бронированию не положен.",
                $"Ссылка действует {minutes} мин. и работает один раз.",
                "Если вы не отправляли запрос, проигнорируйте письмо: бронирование остаётся в силе."),
            _ => ($"Confirm the cancellation of your AEROVA booking ({pnr})",
                $"Hello {name},\n\nWe received a request to cancel booking {pnr} ({flights}). The booking is only cancelled after you open this link and confirm:",
                $"Estimated refund under the fare rules: {Money(refund)}" + (fee > 0 ? $" (cancellation fee {Money(fee)})." : "."),
                "Under the fare rules no refund is due for this booking.",
                $"The link is valid for {minutes} minutes and works once.",
                "If you did not ask for this, ignore this message: your booking stays active.")
        };

        return Build(subject, intro + "\n\n" + (refundDue ? estimate : none), link, validity, ignore, name);
    }

    /// <summary>Lets the person choose a new password by opening the link.</summary>
    public static (string Subject, string Text, string Html) PasswordReset(string? language, string name, string link, int minutes)
    {
        var (subject, intro, validity, ignore) = NormalizeLanguage(language) switch
        {
            "az" => ("AEROVA şifrənizi sıfırlayın",
                $"Salam {name},\n\nAEROVA şifrənizin sıfırlanması üçün sorğu aldıq. Yeni şifrə seçmək üçün bu keçidi açın:",
                $"Keçid {minutes} dəqiqə etibarlıdır və yalnız bir dəfə işləyir.",
                "Bunu siz istəməmisinizsə, məktuba məhəl qoymayın; şifrəniz dəyişməyəcək."),
            "ru" => ("Сброс пароля AEROVA",
                $"Здравствуйте, {name}!\n\nМы получили запрос на сброс пароля AEROVA. Откройте ссылку, чтобы задать новый пароль:",
                $"Ссылка действует {minutes} мин. и работает один раз.",
                "Если вы не отправляли запрос, проигнорируйте письмо: пароль останется прежним."),
            _ => ("Reset your AEROVA password",
                $"Hello {name},\n\nWe received a request to reset your AEROVA password. Open this link to choose a new one:",
                $"The link is valid for {minutes} minutes and works once.",
                "If you did not ask for this, ignore this message; your password stays the same.")
        };

        return Build(subject, intro, link, validity, ignore, name);
    }

    /// <summary>The six-digit code of a password reset, in English, Azerbaijani or Russian. The code is shown large, not as a link.</summary>
    public static (string Subject, string Text, string Html) ResetCode(string? language, string name, string code, int minutes)
    {
        var (subject, intro, validity, ignore) = NormalizeLanguage(language) switch
        {
            "az" => ("AEROVA şifrə sıfırlama kodu",
                $"Salam {name},\n\nAEROVA şifrənizin sıfırlanması üçün sorğu aldıq. Kodunuz:",
                $"Kod {minutes} dəqiqə etibarlıdır və yalnız bir dəfə işləyir. Kodu heç kimlə bölüşməyin.",
                "Bunu siz istəməmisinizsə, məktuba məhəl qoymayın; şifrəniz dəyişməyəcək."),
            "ru" => ("Код для сброса пароля AEROVA",
                $"Здравствуйте, {name}!\n\nМы получили запрос на сброс пароля AEROVA. Ваш код:",
                $"Код действует {minutes} мин. и работает один раз. Никому его не сообщайте.",
                "Если вы не отправляли запрос, проигнорируйте письмо: пароль останется прежним."),
            _ => ("Your AEROVA password reset code",
                $"Hello {name},\n\nWe received a request to reset your AEROVA password. Your code is:",
                $"The code is valid for {minutes} minutes and works once. Do not share it with anyone.",
                "If you did not ask for this, ignore this message; your password stays the same.")
        };

        var text = $"{intro}\n\n{code}\n\n{validity}\n{ignore}\n\nAEROVA";

        var html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:0 auto;padding:24px;color:#0b1a2c\">" +
            "<p style=\"letter-spacing:.2em;color:#9a7844;font-size:13px;margin:0 0 18px\">AEROVA</p>" +
            $"<p style=\"white-space:pre-line;line-height:1.55\">{WebUtility.HtmlEncode(intro)}</p>" +
            $"<p style=\"margin:22px 0;font-size:34px;letter-spacing:.35em;font-weight:bold;font-family:Consolas,Menlo,monospace;color:#0b1a2c\">{WebUtility.HtmlEncode(code)}</p>" +
            $"<p style=\"font-size:13px;color:#555\">{WebUtility.HtmlEncode(validity)}<br>{WebUtility.HtmlEncode(ignore)}</p>" +
            "</div>";

        return (subject, text, html);
    }

    /// <summary>
    /// A notification of the app as an e-mail: the title and text the traveller also sees in AEROVA (they are written in English when
    /// the event happens), with a link to the notifications page.
    /// </summary>
    public static (string Subject, string Text, string Html) Notification(string name, string title, string message, string link)
    {
        var subject = $"AEROVA: {title}";
        var greeting = string.IsNullOrWhiteSpace(name) ? "Hello," : $"Hello {name},";
        var text = $"{greeting}\n\n{message}\n\nSee all your notifications: {link}\n\nAEROVA";

        var html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:0 auto;padding:24px;color:#0b1a2c\">" +
            "<p style=\"letter-spacing:.2em;color:#9a7844;font-size:13px;margin:0 0 18px\">AEROVA</p>" +
            $"<p style=\"margin:0 0 6px\">{WebUtility.HtmlEncode(greeting)}</p>" +
            $"<p style=\"font-size:18px;font-weight:bold;margin:14px 0 6px\">{WebUtility.HtmlEncode(title)}</p>" +
            $"<p style=\"white-space:pre-line;line-height:1.55;margin:0\">{WebUtility.HtmlEncode(message)}</p>" +
            $"<p style=\"margin:22px 0\"><a href=\"{WebUtility.HtmlEncode(link)}\" style=\"background:#c6a36a;color:#0b1a2c;text-decoration:none;padding:12px 22px;border-radius:999px;font-weight:bold\">Open notifications</a></p>" +
            "<p style=\"font-size:12px;color:#888\">You get this message because it is also shown in your AEROVA account.</p>" +
            "</div>";

        return (subject, text, html);
    }

    private static (string Subject, string Text, string Html) Build(string subject, string intro, string link, string validity, string ignore, string name)
    {
        var text = $"{intro}\n{link}\n\n{validity}\n{ignore}\n\nAEROVA";

        var html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:0 auto;padding:24px;color:#0b1a2c\">" +
            "<p style=\"letter-spacing:.2em;color:#9a7844;font-size:13px;margin:0 0 18px\">AEROVA</p>" +
            $"<p style=\"white-space:pre-line;line-height:1.55\">{WebUtility.HtmlEncode(intro)}</p>" +
            $"<p style=\"margin:22px 0\"><a href=\"{WebUtility.HtmlEncode(link)}\" style=\"background:#c6a36a;color:#0b1a2c;text-decoration:none;padding:12px 22px;border-radius:999px;font-weight:bold\">{WebUtility.HtmlEncode(subject)}</a></p>" +
            $"<p style=\"font-size:13px;color:#555\">{WebUtility.HtmlEncode(validity)}<br>{WebUtility.HtmlEncode(ignore)}</p>" +
            $"<p style=\"font-size:12px;color:#888;word-break:break-all\">{WebUtility.HtmlEncode(link)}</p>" +
            "</div>";

        return (subject, text, html);
    }
}
