using System.Globalization;
using System.Net;

namespace Service.Helpers.Mail;

/// <summary>
/// The promotion e-mail. Everything in it comes from the promo code saved in the database (code, discount, dates, usage limit); the page
/// the admin uses sends none of it. This is a marketing e-mail: it always carries the link that opts the subscriber out.
/// </summary>
public static class CampaignEmailTemplates
{
    public static (string Subject, string Text, string Html) Promotion(
        string? language, string code, int discountPercent, DateTime? validFrom, DateTime? validTo, int? maxUses, string pageLink, string unsubscribeLink)
    {
        static string Day(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lang = EmailTemplates.NormalizeLanguage(language);

        var conditions = new List<string>();
        switch (lang)
        {
            case "az":
                conditions.Add("Kodu ödəniş səhifəsində daxil edin: endirim orada hesablanır.");
                if (validFrom is { } from) conditions.Add($"{Day(from)} tarixindən etibarlıdır.");
                if (validTo is { } to) conditions.Add($"{Day(to)} tarixinədək etibarlıdır.");
                if (maxUses is { } uses) conditions.Add($"Məhdud istifadə sayı: {uses}.");
                break;
            case "ru":
                conditions.Add("Введите код на странице оплаты: скидка рассчитывается там.");
                if (validFrom is { } fromRu) conditions.Add($"Действует с {Day(fromRu)}.");
                if (validTo is { } toRu) conditions.Add($"Действует до {Day(toRu)}.");
                if (maxUses is { } usesRu) conditions.Add($"Ограниченное число использований: {usesRu}.");
                break;
            default:
                conditions.Add("Enter the code on the payment page: the discount is calculated there.");
                if (validFrom is { } fromEn) conditions.Add($"Valid from {Day(fromEn)}.");
                if (validTo is { } toEn) conditions.Add($"Valid until {Day(toEn)}.");
                if (maxUses is { } usesEn) conditions.Add($"Limited number of uses: {usesEn}.");
                break;
        }

        var (subject, headline, codeLabel, button, reason, optOut) = lang switch
        {
            "az" => ($"AEROVA: {code} kodu ilə {discountPercent}% endirim", $"AEROVA rezervasiyanızda {discountPercent}% endirim", "Promo kod", "İndi rezerv et",
                "Bu məktubu AEROVA yeniliklərinə abunə olduğunuz üçün alırsınız.", "Abunəlikdən çıx"),
            "ru" => ($"AEROVA: скидка {discountPercent}% по коду {code}", $"Скидка {discountPercent}% на бронирование AEROVA", "Промокод", "Забронировать",
                "Вы получили это письмо, потому что подписаны на новости AEROVA.", "Отписаться"),
            _ => ($"AEROVA: {discountPercent}% off with code {code}", $"{discountPercent}% off your AEROVA booking", "Promo code", "Book now",
                "You receive this because you subscribed to AEROVA news.", "Unsubscribe")
        };

        var text = $"{headline}\n\n{codeLabel}: {code}\n\n{string.Join("\n", conditions)}\n\n{pageLink}\n\n{reason}\n{optOut}: {unsubscribeLink}\n\nAEROVA";

        string E(string value) => WebUtility.HtmlEncode(value);
        var html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:0 auto;padding:24px;color:#0b1a2c\">" +
            "<p style=\"letter-spacing:.2em;color:#9a7844;font-size:13px;margin:0 0 18px\">AEROVA</p>" +
            $"<h2 style=\"margin:0 0 16px;font-size:22px;line-height:1.3\">{E(headline)}</h2>" +
            $"<p style=\"margin:0 0 6px;font-size:13px;color:#555\">{E(codeLabel)}</p>" +
            $"<p style=\"margin:0 0 18px;font-size:24px;font-weight:bold;letter-spacing:.12em;border:2px dashed #c6a36a;display:inline-block;padding:10px 18px\">{E(code)}</p>" +
            $"<p style=\"font-size:14px;line-height:1.6;color:#333\">{string.Join("<br>", conditions.Select(E))}</p>" +
            $"<p style=\"margin:22px 0\"><a href=\"{E(pageLink)}\" style=\"background:#c6a36a;color:#0b1a2c;text-decoration:none;padding:12px 22px;border-radius:999px;font-weight:bold;display:inline-block\">{E(button)}</a></p>" +
            $"<hr style=\"border:none;border-top:1px solid #e5e0d6;margin:22px 0\">" +
            $"<p style=\"font-size:12px;color:#888\">{E(reason)} <a href=\"{E(unsubscribeLink)}\" style=\"color:#888\">{E(optOut)}</a></p>" +
            "</div>";

        return (subject, text, html);
    }
}
