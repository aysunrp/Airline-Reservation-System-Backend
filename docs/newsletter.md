# Newsletter subscription

The home page form subscribes an address in two steps (double opt-in). The mail server is the existing Gmail SMTP configuration
(`Smtp` settings, password in user-secrets or `Smtp__Password`); nothing new is needed for sending.

## Flow

1. `POST /api/newsletter/subscribe` `{ "email": "...", "language": "az|en|ru" }`. The address is stored as **Pending** and a confirmation
   e-mail is sent. The answer is always `{ "status": "confirmation_sent" }`, whether the address is new, waiting, already subscribed or
   was unsubscribed, so the form cannot be used to find out who is on the list. An already subscribed address gets an information e-mail
   (with the unsubscribe link) instead of a confirmation link. A repeated request within `Newsletter:ResendCooldownMinutes` (2) sends no
   second e-mail. When the e-mail cannot be sent the answer is `503` (never a false success) and the next request tries again at once.
2. `POST /api/newsletter/confirm` `{ "token": "..." }`: the link in the e-mail opens `newsletter-confirm.html?token=...`. The token is
   256 random bits; only its SHA-256 hash is stored. It works once and for `Newsletter:ConfirmationHours` (48). The address becomes
   **Active**. A used, unknown or expired link answers `422` with code `link_invalid` or `link_expired`; the page then offers a new request.
3. `POST /api/newsletter/unsubscribe` `{ "token": "..." }`: every e-mail carries `newsletter-unsubscribe.html?token=...`. The token is the
   subscriber's random public key plus a signature that only the server can make (HMAC with a key derived from the JWT secret), checked
   before the database is asked. No login is needed. Opening it again changes nothing. The address is **Unsubscribed** and stays out
   until it subscribes again **and** confirms again.

Subscribe, confirm and unsubscribe share one rate limit per IP address (`Newsletter:RateLimitPerMinute`, 10). Tokens, addresses and
e-mail contents are never logged.

## Who may receive a newsletter

`INewsletterService.GetRecipientsAsync()` returns only Active subscribers, each with its unsubscribe link. A newsletter sender must take
its recipients from there. **No newsletter campaign or admin sending feature exists yet**: AEROVA has to write the content and a
sending feature (for example an admin endpoint that builds the message and sends it to those recipients) is a separate piece of work.
Nothing is sent to subscribers except the confirmation and information e-mails.

## Database

Table `NewsletterSubscribers` (migration `AddNewsletterSubscribers`): unique normalized `Email`, `Status` (Pending, Active,
Unsubscribed), `Language`, `ConfirmationTokenHash` (unique, empty once used), `ConfirmationExpiresAt`, `ConfirmationSentAt`,
`ConfirmedAt`, `UnsubscribedAt`, `UnsubscribeKey` (unique). Apply the migration with `dotnet ef database update` before using the feature.
