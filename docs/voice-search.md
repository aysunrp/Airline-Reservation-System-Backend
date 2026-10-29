# Voice flight search

The home page search card takes a spoken or typed sentence. **The speech-to-text is the browser's Web Speech API** (`SpeechRecognition` /
`webkitSpeechRecognition`), in the page; the server never hears audio. The recognised text goes to the API, which reads it into search
criteria. The flights always come from the real schedules, fares and seats; nothing is invented.

## Endpoints

### `POST /api/voice-search/interpret`

Open to visitors, limited per IP address like the other look-ups (`Auth:LookupRateLimitPerMinute`).

```json
{ "text": "Bakından Londona sabah uçuşları göstər", "language": "az" }
```

Answers `200` with the criteria (it only reads; it never searches or books):

| Field | Meaning |
| --- | --- |
| `status` | `ready`, or `needs_clarification` when `issues` is not empty |
| `origin`, `destination` | `{ code, city, name }` from the active airports |
| `date` | one departure day (airport time of the origin) |
| `dateFrom`, `dateTo` | a period (cheapest flight, "next week", "in November", "next 30 days", or no day at all) |
| `defaultPeriod` | true when no day was said and the coming `VoiceSearch:DefaultRangeDays` days were taken |
| `timeOfDay` | `Morning`, `Afternoon`, `Evening`, `Night` |
| `cabinClass`, `passengers`, `passengersSpecified` | cabin if said; passengers default to 1 (`passengersSpecified` false) |
| `cheapest` | the traveller asked for the cheapest flight |
| `cheapestSingle` | the request is for the one cheapest flight ("the cheapest flight"); false for a plural request ("the cheapest flights"), which keeps several low-priced options |
| `interpretedBy` | `rules` (fixed rules, always tried first) or `assistant` |
| `issues` | `missing_text`, `missing_origin`, `missing_destination`, `same_airport`, `ambiguous_origin` / `ambiguous_destination` (with `options`), `unknown_origin` / `unknown_destination` (with the name), `past_date`, `invalid_date`, `ambiguous_date`, `bad_passengers` |

Reading is done by fixed rules in Azerbaijani, English and Russian (`Service/Services/VoiceSearch`): city names with their case endings
("Bakıdan", "Londona", "из Москвы"), exact dates ("noyabrın 15-i", "November 15", "15 ноября", `2026-11-15`, `15.11.2026`), relative days
(tomorrow, next Friday, "gələn cümə", "в пятницу"), periods, parts of the day, cabins and passenger counts. "Today" is today at the origin
airport. A date in the past is refused, never moved; a day without a year is its next occurrence; an ambiguous `05/11/2026` in English is
asked about.

Only when the rules find no route at all, and `TravelAssistant` (Gemini) is set up, the assistant is asked to read the sentence. Its
answer is checked against the real airports, today's date and the allowed values before any of it is used. If the assistant is not set up,
is down, or answers badly, the rules' answer stands, so voice and typed search keep working without any AI key.
`VoiceSearch:UseAssistantForComplexRequests=false` switches this off.

### `POST /api/flights/search-range`

The real bookable flights of one route over a period, cheapest first (`sortBy: "price"`, default) or earliest first (`"departure"`),
optionally one part of the day (`timeOfDay`) and one cabin. Same rules as the ordinary search (flights that have left, flights without
enough free seats, and flights without the cabin are left out). At most `VoiceSearch:MaxRangeDays` (62) days and `VoiceSearch:MaxResults`
(50) flights; `totalFound` is the true count. `limit` (1 to `MaxResults`) cuts the list after the whole period has been compared, so `sortBy: "price"` with `limit: 1` is the one cheapest flight of the period. Equal prices are ordered by departure, then flight id, so the same flight is always chosen; `lowestPriceCount` says how many flights share the lowest price.

Parts of the day, at the origin airport: morning 05:00-11:59, afternoon 12:00-17:59, evening 18:00-23:59, night 00:00-04:59.

## Settings (`VoiceSearch`, not secret)

| Setting | Default | Meaning |
| --- | --- | --- |
| `DefaultRangeDays` | `30` | Days searched for the cheapest flight, or when no day was said (today included) |
| `MaxRangeDays` | `62` | Widest period of one search |
| `MaxResults` | `50` | Most flights returned by a period search |
| `MaxTextLength` | `300` | Longest sentence that is read |
| `UseAssistantForComplexRequests` | `true` | Let Gemini read sentences the rules cannot (needs `TravelAssistant:ApiKey`) |

No key is needed for voice search. The Gemini key stays on the server (user-secrets or environment), as for Ask AEROVA.
