# E-mail notifications and campaigns

All e-mail goes through the one existing sender (`IEmailSender`, Gmail SMTP with an App password in user-secrets or `Smtp__Password`).
Nothing is sent while `Smtp` is incomplete, and a failed send never loses the business record it reports.

## To customers (already existing, unchanged)

Registration verification link, password reset code, booking confirmation link (before payment), booking confirmed and payment received,
cancellation link and confirmation, refund started / completed / failed (sent only when Stripe's confirmed status says so). Notifications use an
`EmailKey`, so a Stripe webhook that is delivered twice never sends a second e-mail.

## To the admin (`AdminNotifications`)

Table `AdminAlerts`: a message is stored with the event (unique `EventKey`, so retried webhooks add nothing) and sent by the background job
(`NotificationEmailWorker`, every 20 s, up to `MaxAttempts` tries). Recipient: `AdminNotifications:RecipientEmail`, or `Smtp:SenderEmail` when empty;
`AdminNotifications:Enabled=false` switches it off.

| Event | Where it is raised |
| --- | --- |
| New booking created (payment still pending) | `BookingService` |
| Booking confirmed, payment confirmed | `NotificationService` (same events as the customer notification, so only what the backend/Stripe confirmed) |
| Cancellation requested (link e-mailed) | `CancellationLinkService` |
| Booking cancelled; refund processing / succeeded / FAILED | `NotificationService` |
| New newsletter subscription confirmed | `NewsletterService.ConfirmAsync` |
| A customer e-mail given up after its last attempt; a campaign finished | `NotificationEmailService`, `CampaignService` |

## Promotion campaigns

Saving or editing a promo code **never** sends e-mail. In the admin page, a promo code row has an **Email campaign** button:

1. `GET /api/admin/promo-codes/{id}/campaign-preview?language=az|en|ru`: the e-mail built from the saved promotion (code, discount, dates, usage
   limit), how many confirmed subscribers get it, problems (`promo_inactive`, `promo_expired`, `promo_exhausted`, `no_subscribers`,
   `campaign_already_sent`) and `previewHash`.
2. `POST /api/admin/promo-codes/{id}/campaign` `{ "previewHash": "...", "confirm": true }`: the admin ticks "Send email campaign to subscribed
   customers" and confirms. The hash must match the promotion as it is now (409 otherwise); the discount is never taken from the request. A
   promo code has **one** campaign (unique index), so a second click or another admin gets 409. Answer `202`, status `Queued`.
3. `EmailCampaignWorker` (every 15 s, `Newsletter:CampaignBatchSize` recipients per run) sends in the background. Each recipient is checked again
   at send time: only an **Active** (confirmed, not unsubscribed) subscriber is sent to; others are `Skipped`. A failed delivery is retried up to
   `Newsletter:CampaignMaxAttempts`, then `Failed`; the campaign ends `Completed` or `CompletedWithErrors` and the admin is told.
4. `GET /api/admin/campaigns` and `/{id}`: counts of sent, failed, skipped and waiting recipients.

Every campaign e-mail carries the subscriber's own unsubscribe link. Tables: `EmailCampaigns`, `EmailCampaignRecipients`.

## Database

Migration `AddEmailCampaignsAndAdminAlerts` creates three new tables and changes nothing that exists. It is **not applied** by the code; apply it
with `dotnet ef database update --no-build --project Repository --startup-project AirlineReservationSystem` before using campaigns and admin alerts.
Until then the booking, payment and refund flows are not affected: admin alerts are skipped with a warning in the log (the alert lookup fails safely
and nothing is added to the unit of work of the event), and the campaign endpoints answer with an error.
