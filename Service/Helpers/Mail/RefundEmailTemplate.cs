using System.Globalization;
using System.Net;
using System.Text;

namespace Service.Helpers.Mail;

/// <summary>Which moment of a cancellation the e-mail tells about. Each says only what is true at that moment.</summary>
public enum RefundEmailKind
{
    Cancelled,
    RefundPending,
    RefundCompleted,
    RefundFailed
}

public record RefundEmailFlight(string FlightNumber, string From, string To, DateTime DepartureAt);

/// <summary>Everything the e-mail shows. All amounts come from the cancellation the server saved, never from a page.</summary>
public record RefundEmailDetails(
    string Pnr,
    IReadOnlyList<RefundEmailFlight> Flights,
    DateTime CancelledAt,
    string? Reason,
    decimal AmountPaid,
    decimal FeeAmount,
    decimal RefundAmount,
    string Currency,
    bool RefundDue,
    bool PaidByCard,
    string Link);

/// <summary>
/// The cancellation and refund e-mails in English, Azerbaijani and Russian, in the navy and gold of AEROVA. The layout is one column
/// of tables (what mail programs on phones show reliably). "5 to 10 business days" is said to be usual, never promised, and a refund
/// is only called confirmed in the e-mail sent after Stripe confirmed it.
/// </summary>
public static class RefundEmailTemplate
{
    private sealed record Texts(
        string Hello, string Reference, string Flights, string CancelledOn, string Reason, string Paid, string Fee, string Refund,
        string NotRefunded, string Status, string Method, string MethodCard, string MethodOther, string Button, string Support, string Footer,
        string SubjectCancelled, string SubjectPending, string SubjectCompleted, string SubjectFailed,
        string TitleCancelled, string TitlePending, string TitleCompleted, string TitleFailed,
        string StatusRequested, string StatusNone, string StatusPending, string StatusCompleted, string StatusFailed,
        string BodyRequested, string BodyNone, string BodyPending, string BodyCompleted, string BodyFailed, string Late);

    private static readonly Texts En = new(
        "Hello {name},", "Booking reference (PNR)", "Cancelled flights", "Cancelled on", "Reason", "Amount paid", "Cancellation fee",
        "Refund", "Not refunded", "Refund status", "Refunded to", "the card you paid with", "the original payment method", "Open My Trips",
        "Questions? Reply to this e-mail or open My Trips in your AEROVA account. Please never send card details by e-mail.",
        "You get this e-mail because the booking was made with your AEROVA account.",
        "Your AEROVA booking {pnr} is cancelled", "Your AEROVA refund for {pnr} has started", "Your AEROVA refund for {pnr} is confirmed",
        "Your AEROVA refund for {pnr} could not be completed",
        "Your booking is cancelled", "Your refund has started", "Your refund is confirmed", "Your refund could not be completed",
        "Requested, waiting for confirmation", "No refund due under the fare rules", "Being processed", "Confirmed by Stripe", "Not completed",
        "Your booking has been cancelled. A refund of {refund} has been requested to {method}. Nothing has been paid back yet: we will e-mail you again as soon as the refund is confirmed.",
        "Your booking has been cancelled. Under the fare rules of your ticket no refund is due.",
        "The refund of {refund} has been started to {method}, and Stripe is still processing it. Once it is done, it usually takes 5 to 10 business days, depending on your bank, to appear on your statement. You can follow the refund status in your AEROVA account.",
        "Stripe has confirmed the refund of {refund} to {method}. Depending on your bank it usually takes 5 to 10 business days to appear on your statement; this is an estimate, not a guarantee. You can follow the refund status in your AEROVA account.",
        "We could not complete the refund of {refund}. Your booking stays cancelled and this amount is still owed to you; nothing has been paid back yet. Please reply to this e-mail or contact AEROVA support with booking reference {pnr}, and we will arrange the refund.",
        "If the amount has not appeared after 10 business days, please contact your bank first and, if needed, AEROVA support.");

    private static readonly Texts Az = new(
        "Salam {name},", "Rezervasiya kodu (PNR)", "Ləğv edilən uçuşlar", "Ləğv tarixi", "Səbəb", "Ödənilən məbləğ", "Ləğv cəriməsi",
        "Qaytarılacaq məbləğ", "Qaytarılmayan məbləğ", "Refund statusu", "Qaytarılır", "ödəniş etdiyiniz karta", "ilkin ödəniş üsuluna", "Səyahətlərimi aç",
        "Sualınız var? Bu məktuba cavab yazın və ya AEROVA hesabınızda Səyahətlərim bölməsini açın. Kart məlumatlarını heç vaxt e-poçtla göndərməyin.",
        "Bu məktubu rezervasiya AEROVA hesabınızla edildiyi üçün alırsınız.",
        "AEROVA rezervasiyanız {pnr} ləğv edildi", "{pnr} üzrə AEROVA refund-u başladıldı", "{pnr} üzrə AEROVA refund-u təsdiqləndi",
        "{pnr} üzrə AEROVA refund-u tamamlanmadı",
        "Rezervasiyanız ləğv edildi", "Pulun geri qaytarılması başladıldı", "Pulun geri qaytarılması təsdiqləndi", "Pulun geri qaytarılması tamamlanmadı",
        "Sorğu göndərilib, təsdiq gözlənilir", "Tarif qaydalarına görə qaytarılma yoxdur", "Emal olunur", "Stripe tərəfindən təsdiqlənib", "Tamamlanmayıb",
        "Rezervasiyanız ləğv edildi. {refund} məbləğinin {method} qaytarılması üçün sorğu göndərilib. Hələ heç bir məbləğ qaytarılmayıb: refund təsdiqlənən kimi sizə yenidən məktub yazacağıq.",
        "Rezervasiyanız ləğv edildi. Biletinizin tarif qaydalarına görə qaytarılacaq məbləğ yoxdur.",
        "Pulun geri qaytarılması ilkin ödəniş zamanı istifadə etdiyiniz karta başladılıb və Stripe tərəfindən hələ emal olunur. Emal bitdikdən sonra məbləğin bank çıxarışınızda görünməsi bankınızdan asılı olaraq adətən 5–10 iş günü çəkə bilər. Bu müddətdə refund statusunu AEROVA hesabınızdan izləyə bilərsiniz.",
        "Stripe {refund} məbləğinin ilkin ödəniş zamanı istifadə etdiyiniz karta qaytarılmasını təsdiqlədi. Məbləğin bank çıxarışınızda görünməsi bankınızdan asılı olaraq adətən 5–10 iş günü çəkə bilər; bu, təxmini müddətdir, zəmanət deyil. Refund statusunu AEROVA hesabınızdan izləyə bilərsiniz.",
        "{refund} məbləğinin qaytarılmasını tamamlamaq mümkün olmadı. Rezervasiyanız ləğv edilmiş qalır və bu məbləğ sizə hələ də borcludur; hələ heç nə qaytarılmayıb. Bu məktuba cavab yazın və ya {pnr} rezervasiya kodu ilə AEROVA dəstəyinə müraciət edin, qaytarılmanı təşkil edəcəyik.",
        "Məbləğ 10 iş günündən sonra da görünməsə, əvvəlcə bankınızla, lazım olduqda isə AEROVA dəstəyi ilə əlaqə saxlayın.");

    private static readonly Texts Ru = new(
        "Здравствуйте, {name}!", "Код бронирования (PNR)", "Отменённые рейсы", "Дата отмены", "Причина", "Оплачено", "Сбор за отмену",
        "К возврату", "Не возвращается", "Статус возврата", "Возврат на", "карту, которой вы платили", "исходный способ оплаты", "Открыть мои поездки",
        "Есть вопросы? Ответьте на это письмо или откройте «Мои поездки» в аккаунте AEROVA. Никогда не отправляйте данные карты по почте.",
        "Вы получили это письмо, потому что бронирование сделано в вашем аккаунте AEROVA.",
        "Бронирование AEROVA {pnr} отменено", "Возврат AEROVA по {pnr} начат", "Возврат AEROVA по {pnr} подтверждён",
        "Возврат AEROVA по {pnr} не выполнен",
        "Бронирование отменено", "Возврат начат", "Возврат подтверждён", "Возврат не выполнен",
        "Запрошен, ожидает подтверждения", "По правилам тарифа возврат не положен", "Обрабатывается", "Подтверждён Stripe", "Не выполнен",
        "Бронирование отменено. Запрошен возврат {refund} на {method}. Деньги ещё не возвращены: мы напишем снова, как только возврат будет подтверждён.",
        "Бронирование отменено. По правилам тарифа вашего билета возврат не положен.",
        "Возврат начат на карту, которой вы оплачивали бронирование, и ещё обрабатывается Stripe. После обработки сумма обычно появляется в выписке через 5–10 рабочих дней, в зависимости от банка. Статус возврата можно отслеживать в аккаунте AEROVA.",
        "Stripe подтвердил возврат {refund} на карту, которой вы оплачивали бронирование. В зависимости от банка сумма обычно появляется в выписке через 5–10 рабочих дней; это ориентировочный срок, а не гарантия. Статус возврата можно отслеживать в аккаунте AEROVA.",
        "Не удалось выполнить возврат {refund}. Бронирование остаётся отменённым, и эта сумма по-прежнему причитается вам; деньги ещё не возвращены. Ответьте на это письмо или обратитесь в поддержку AEROVA с кодом бронирования {pnr}, и мы организуем возврат.",
        "Если сумма не появилась через 10 рабочих дней, сначала обратитесь в свой банк, а при необходимости — в поддержку AEROVA.");

    public static (string Subject, string Text, string Html) Build(string? language, RefundEmailKind kind, string name, RefundEmailDetails d)
    {
        var t = EmailTemplates.NormalizeLanguage(language) switch { "az" => Az, "ru" => Ru, _ => En };

        string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture) + " " + d.Currency.ToUpperInvariant();
        string Fill(string text) => text
            .Replace("{pnr}", d.Pnr)
            .Replace("{refund}", Money(d.RefundAmount))
            .Replace("{method}", d.PaidByCard ? t.MethodCard : t.MethodOther)
            .Replace("{name}", name);

        var (subject, title, status, body, late) = kind switch
        {
            RefundEmailKind.RefundPending => (t.SubjectPending, t.TitlePending, t.StatusPending, t.BodyPending, true),
            RefundEmailKind.RefundCompleted => (t.SubjectCompleted, t.TitleCompleted, t.StatusCompleted, t.BodyCompleted, true),
            RefundEmailKind.RefundFailed => (t.SubjectFailed, t.TitleFailed, t.StatusFailed, t.BodyFailed, false),
            _ when d.RefundDue => (t.SubjectCancelled, t.TitleCancelled, t.StatusRequested, t.BodyRequested, false),
            _ => (t.SubjectCancelled, t.TitleCancelled, t.StatusNone, t.BodyNone, false)
        };

        var greeting = string.IsNullOrWhiteSpace(name) ? Fill(t.Hello).Replace(" ,", ",").Replace("  ", " ") : Fill(t.Hello);
        var flights = d.Flights.Select(f => $"{f.FlightNumber} · {f.From} → {f.To} · {f.DepartureAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC").ToList();
        var notRefunded = Math.Max(0, d.AmountPaid - d.RefundAmount);

        var rows = new List<(string Label, string Value)>
        {
            (t.Reference, d.Pnr),
            (t.CancelledOn, d.CancelledAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC")
        };
        if (!string.IsNullOrWhiteSpace(d.Reason))
        {
            rows.Add((t.Reason, d.Reason!));
        }
        rows.Add((t.Paid, Money(d.AmountPaid)));
        if (d.FeeAmount > 0)
        {
            rows.Add((t.Fee, Money(d.FeeAmount)));
        }
        rows.Add((t.Refund, Money(d.RefundAmount)));
        if (notRefunded > 0 && d.RefundAmount > 0)
        {
            // A partial refund: what is kept under the fare rules is shown next to what comes back.
            rows.Add((t.NotRefunded, Money(notRefunded)));
        }
        rows.Add((t.Status, status));
        if (d.RefundDue)
        {
            rows.Add((t.Method, d.PaidByCard ? t.MethodCard : t.MethodOther));
        }

        var text = new StringBuilder();
        text.Append(greeting).Append("\n\n").Append(title).Append("\n\n").Append(Fill(body)).Append("\n\n");
        foreach (var (label, value) in rows)
        {
            text.Append(label).Append(": ").Append(value).Append('\n');
        }
        text.Append(t.Flights).Append(":\n");
        foreach (var flight in flights)
        {
            text.Append("  ").Append(flight).Append('\n');
        }
        if (late)
        {
            text.Append('\n').Append(t.Late).Append('\n');
        }
        text.Append('\n').Append(t.Button).Append(": ").Append(d.Link).Append("\n\n").Append(t.Support).Append("\n\nAEROVA");

        static string E(string value) => WebUtility.HtmlEncode(value);
        var html = new StringBuilder();
        html.Append("<div style=\"margin:0;padding:0;background:#eef0f3\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#eef0f3;padding:16px 0\"><tr><td align=\"center\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;background:#ffffff;border-radius:14px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;color:#0b1a2c\">")
            .Append("<tr><td style=\"background:#0b1a2c;padding:22px 24px\"><span style=\"color:#c6a36a;letter-spacing:.32em;font-size:15px;font-weight:bold\">AEROVA</span></td></tr>")
            .Append("<tr><td style=\"padding:24px 24px 8px\">")
            .Append("<p style=\"margin:0 0 6px;font-size:15px\">").Append(E(greeting)).Append("</p>")
            .Append("<h1 style=\"margin:12px 0 10px;font-size:21px;line-height:1.3;color:#0b1a2c\">").Append(E(title)).Append("</h1>")
            .Append("<p style=\"margin:0;font-size:15px;line-height:1.6\">").Append(E(Fill(body))).Append("</p>")
            .Append("</td></tr>")
            .Append("<tr><td style=\"padding:14px 24px\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border:1px solid #e4d6bd;border-radius:10px;font-size:14px\">");
        foreach (var (label, value) in rows)
        {
            html.Append("<tr><td style=\"padding:9px 12px;color:#6b6f78;border-bottom:1px solid #f1e8d8\">").Append(E(label))
                .Append("</td><td align=\"right\" style=\"padding:9px 12px;font-weight:bold;border-bottom:1px solid #f1e8d8\">").Append(E(value)).Append("</td></tr>");
        }
        html.Append("</table></td></tr>")
            .Append("<tr><td style=\"padding:4px 24px 0\"><p style=\"margin:8px 0 6px;font-size:12px;letter-spacing:.12em;text-transform:uppercase;color:#9a7844\">").Append(E(t.Flights)).Append("</p>");
        foreach (var flight in flights)
        {
            html.Append("<p style=\"margin:0 0 4px;font-size:14px\">").Append(E(flight)).Append("</p>");
        }
        html.Append("</td></tr>");
        if (late)
        {
            html.Append("<tr><td style=\"padding:12px 24px 0\"><p style=\"margin:0;font-size:13px;line-height:1.55;color:#555\">").Append(E(t.Late)).Append("</p></td></tr>");
        }
        html.Append("<tr><td style=\"padding:22px 24px\"><a href=\"").Append(E(d.Link)).Append("\" style=\"display:inline-block;background:#c6a36a;color:#0b1a2c;text-decoration:none;padding:12px 24px;border-radius:999px;font-weight:bold;font-size:14px\">")
            .Append(E(t.Button)).Append("</a></td></tr>")
            .Append("<tr><td style=\"padding:0 24px 22px\"><p style=\"margin:0 0 8px;font-size:13px;line-height:1.55;color:#444\">").Append(E(t.Support)).Append("</p>")
            .Append("<p style=\"margin:0;font-size:12px;color:#888\">").Append(E(t.Footer)).Append("</p></td></tr>")
            .Append("</table></td></tr></table></div>");

        return (Fill(subject), text.ToString(), html.ToString());
    }
}
