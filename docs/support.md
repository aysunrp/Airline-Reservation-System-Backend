# Customer support

Support requests ("tickets") with a conversation between the customer and AEROVA staff. Not to be confused with the flight `Ticket` entity.

## Customer endpoints (`/api/support`)

| Endpoint | Purpose |
| --- | --- |
| `GET info` (open) | Contact details to show: only `Support:ContactEmail`, `null` when it is not configured. No phone number or hours are made up. |
| `POST tickets` | Opens a request: `category`, `subject` (max 120), `message` (10-4000), optional `bookingPnr` (5-10 letters/digits). Rate limited per customer. |
| `GET tickets` | The signed-in customer's requests. |
| `GET tickets/{id}` | One request with its conversation. Somebody else's request answers 404, like one that does not exist. |
| `POST tickets/{id}/replies` | A reply. 409 on a resolved request (a new one is opened instead). |

The owner is always the account in the sign-in token; no request carries a user id.

## Staff endpoints (`/api/admin/support/tickets`, Admin and SuperAdmin only)

`GET` (search by ticket number, customer e-mail, subject or booking reference; filter `status`, `category`; `page`, `pageSize` up to 100),
`GET {id}`, `POST {id}/replies` (`message`, optional `status`, default WaitingForCustomer), `PUT {id}/status`. No new role was added.

Statuses: Open, InProgress, WaitingForCustomer, Resolved. A customer reply sets WaitingForCustomer to Open (a request in progress stays in
progress). Staff can set any status, including reopening a resolved request.

## E-mails

Every event stores a notification (key `support:{ticketId}:...`, unique) in the same save as the ticket; the existing notification job sends it
to the customer's verified account address with retries, linking to `support.html`. A new request and a customer reply also queue an admin alert
(`AdminNotifications:RecipientEmail`). A mail server that is down never loses or blocks a ticket, a repeated request or reply (same text within
`Support:DuplicateWindowSeconds`) sends nothing twice, and the page never says an e-mail was delivered. Notification e-mails are in English, as
the other notification e-mails are.

## Settings (`Support`)

`ContactEmail` (empty: none shown), `RateLimitPerMinute` (10), `DuplicateWindowSeconds` (120), `MaxOpenTicketsPerUser` (20).

## Database

Migration `AddSupportTickets` creates `SupportTickets` and `SupportMessages` (new tables only). It is not applied by the code:
`dotnet ef database update --no-build --project Repository --startup-project AirlineReservationSystem`. Until then the support endpoints answer 500.
