# Admin roles, dashboard numbers and delete rules

Roles come from the signed token and are checked on the server (policies `Admin` and `SuperAdmin`); the page only hides what the server would refuse.
The first SuperAdmin is created as described in `superadmin-setup.md`. No default account or password exists, and no request can create a SuperAdmin.

## Who may do what

| Area | Admin | SuperAdmin |
|---|---|---|
| Dashboard, lists, bookings (view, cancel), passengers, refunds (view), support (reply, status), campaigns | yes | yes |
| Create and edit flights, routes, schedules, aircraft, airports, promo codes, baggage and meal options | yes | yes |
| Cancellation rules (create, edit, delete) | read only | yes |
| Delete flights, routes, schedules, aircraft, airports, promo codes, options, rules | no (403) | yes, subject to the rules below |
| Create flights from schedules, retry refunds | no (403) | yes |
| Users: list, create Admin, change role, activate or deactivate, delete | no (403) | yes (never yourself, never the last active SuperAdmin) |
| Audit log | no (403) | yes |

## What a delete does

| Record | Delete request ends as |
|---|---|
| Promo code | never used: deleted (204). Used by bookings: kept and deactivated (200, outcome `Deactivated`) |
| Flight | no bookings: deleted. Has bookings: refused (422), set its status to Cancelled instead |
| Schedule | no created flights: deleted. Has created flights: refused (422), set it inactive |
| Route, airport, aircraft, baggage option, meal option | not referenced: deleted. Referenced: refused (422), set it inactive |
| Cancellation rule | deleted (reference data only) |
| User | no bookings: deleted (audited). Has bookings: refused (422), deactivate instead |
| Booking, payment, refund, audit entry, support ticket | no delete endpoint: bookings are cancelled, refunds retried, tickets resolved. History stays |

Database messages are never sent to the browser.

## Dashboard numbers

| Card | Counts |
|---|---|
| Total flights | every flight record, past and cancelled included |
| Active routes | routes that are switched on, each once |
| Aircraft | aircraft records |
| Paid bookings | bookings that are Confirmed or Completed (not Pending, Cancelled or Expired) |
| Available seats | free seats (and expired holds) on upcoming, non-cancelled flights |
| Passengers | distinct travellers, counted once by document |

The recent-bookings table shows the newest five bookings of any status, the upcoming-flights table the next five non-cancelled departures.
No migration is needed for any of this.
