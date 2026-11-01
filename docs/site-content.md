# Page texts (SiteContent)

The texts of the "AEROVA haqqında" page (`about-aerova.html`) are kept in the database so an admin can edit them. AEROVA is an
Azerbaijani-only site, so a text has **one** value: `TextAz`. There is no English or Russian text.

## Data

Table `SiteContents`: `Id`, `Page` (for example `about`), `Key` (for example `heroTitle`), `TextAz` (at most 1000 characters), `CreatedAt`,
`UpdatedAt`. `(Page, Key)` is unique. A text is edited, never created or deleted from the admin panel: the page looks its texts up by key.

Defaults live in `Service/Helpers/Seeding/SiteContentData.cs` (the 78 texts of the page). The seeder runs when the API starts (only once the
migrations are applied) and adds **only the keys that are missing**; it never overwrites a text an admin has edited.

## Endpoints

| Endpoint | Who | What |
| --- | --- | --- |
| `GET /api/site-content/{page}` | everyone | all texts of the page: `[{ id, page, key, textAz }]`. `404` when the page has no texts |
| `GET /api/admin/site-content?page=about` | Admin, SuperAdmin | the same list for the editor (`page` defaults to `about`) |
| `PUT /api/admin/site-content/{id}` | Admin, SuperAdmin | body `{ "textAz": "..." }`; answers the changed text. The edit is written to the audit log (`SiteContentUpdated`, `about/<key>`) |

Without a token the admin endpoints answer `401`, for a signed-in customer `403`.

## Messages (Azerbaijani)

| Case | Status | Message |
| --- | --- | --- |
| text missing, empty or only spaces | `400` | `Mətn boş ola bilməz.` |
| more than 1000 characters | `400` | `Mətn 1000 simvoldan uzun ola bilməz. İndi N simvol var.` |
| unknown id | `404` | `Mətn tapılmadı.` |
| page without texts | `404` | `Bu səhifənin mətnləri tapılmadı.` |

The text is trimmed before it is saved. A text may contain markup; it is stored as it is and only ever shown as plain text (the page writes it
with `textContent`, the admin table escapes it).

## The page

`assets/js/site-content.js` asks `GET /api/site-content/about` and replaces the text of every element whose `data-i18n` is `brand.<key>`.
The page ships with the same Azerbaijani texts (`assets/js/locales/az.js`), so while the API is slow or unreachable the page shows them and
nothing is empty or broken.

## The admin panel

"Səhifə mətnləri" lists the keys with their text, a search box filters them, and "Redaktə" opens a form with one field. An empty text or a
text over 1000 characters is refused in the form, in Azerbaijani, before anything is sent.

## Tests

`Service.Tests/SiteContentTests.cs` (service, seeder, validator) and `AirlineReservationSystem.Tests/SiteContentEndpointTests.cs`
(endpoints, authorization, audit, messages).

## Migrations

`AddSiteContent` creates the table. `RemoveSiteContentEnRuTexts` drops the two obsolete columns `TextEn` and `TextRu` and nothing else; the
Azerbaijani texts are not touched. A database that already has the first migration needs the second one before the API can add missing
texts (until then the seeder is skipped and reading and editing keep working).
