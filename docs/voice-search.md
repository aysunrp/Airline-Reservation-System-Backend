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
| `cabinClass` | the cabin, if one was said |
| `passengers`, `passengersSpecified` | **everyone who travels** (adults + children + infants); 1 and `passengersSpecified` false when nobody was counted |
| `adults`, `children`, `infants` | the same people by kind. A plain "iki nəfər" is two adults. Seats are `adults + children`: an infant travels on an adult's lap |
| `cheapest` | the traveller asked for the cheapest flight |
| `cheapestSingle` | the request is for the one cheapest flight ("the cheapest flight"); false for a plural request ("the cheapest flights"), which keeps several low-priced options |
| `fastest` | the traveller asked for the fastest (shortest) flight. When `cheapest` is also true, the cheapest wins |
| `stops` | `direct` (no stop), `connecting` (with a stop or a transfer) or absent (no preference, also when both were named) |
| `returnDate` | the return day of a round trip. Present only together with an exact `date` |
| `interpretedBy` | `rules` (fixed rules, always tried first) or `assistant` |
| `issues` | what has to be cleared up first, see below |

All the new fields are optional additions: a client that does not know them keeps working as before.

#### Issue codes

`missing_text`, `missing_origin`, `missing_destination`, `same_airport`, `ambiguous_origin` / `ambiguous_destination` (with `options`),
`past_date`, `invalid_date`, `ambiguous_date`, `bad_passengers` (`value` is the number), and:

| Code | When | `value` |
| --- | --- | --- |
| `unknown_origin`, `unknown_destination` | a place that is not in the city list ("Atlantidaya", "Atlantidadan"). It is not "a city with no flights": that city is found, and the search simply returns no flights | the name as it was said |
| `missing_return_date` | a round trip ("gedib-gəlmə", "gediş-dönüş") or a "qayıdış" without its return day | |
| `return_before_departure` | the return day is before the departure day | the return day, `yyyy-MM-dd` |
| `return_needs_departure_date` | a return day with no exact departure day (a period, "the cheapest", no day) | |
| `missing_adult` | a child or an infant with no adult | |
| `too_many_infants` | more infants than adults (each infant sits on an adult's lap) | the number of infants |

The page turns each code into a sentence in Azerbaijani (`voice.issues.*` in `assets/js/locales/az.js`). A place that is not in the list and a
place without flights say different things: the first is an issue here, the second is the empty result of the search.

### What is read, and in which language

Reading is done by fixed rules (`Service/Services/VoiceSearch`): city names with their case endings ("Bakıdan", "Londona", "из Москвы"),
exact dates ("noyabrın 15-i", "November 15", "15 ноября", `2026-11-15`, `15.11.2026`), relative days (tomorrow, next Friday, "gələn cümə",
"в пятницу"), periods, parts of the day, cabins and passenger counts. "Today" is today at the origin airport. A date in the past is refused,
never moved; a day without a year is its next occurrence; an ambiguous `05/11/2026` in English is asked about.

The wishes that were added later are read **in Azerbaijani only** (the site is in Azerbaijani); the rules for English and Russian are as they were:

| Said | Read as |
| --- | --- |
| "ən sürətli uçuş", "ən qısa uçuş" | `fastest` |
| "birbaşa", "aktarmasız", "transfersiz", "dayanacaqsız" | `stops: direct` |
| "transferli", "aktarmalı", "dayanacaqlı" | `stops: connecting` |
| "birbaşa və ya transferli" | no preference |
| "10 noyabr gediş 15 noyabr qayıdış", "10 noyabr, qayıdış 15 noyabr", "noyabrın 10-da gedib 15-də qayıdıram", "10.11.2026 gediş 15.11.2026 qayıdış" | `date` 10 Nov, `returnDate` 15 Nov. A day without a month ("15-də") is in the month of the departure (the next month when it is an earlier day). The day written next to the word "qayıdış" is the return day |
| "iki böyük, bir uşaq, bir körpə" | `adults` 2, `children` 1, `infants` 1, `passengers` 4 |
| "2 gün sonra", "üç gün sonra", "iki həftə sonra" | one day, that many days (weeks) after today at the origin airport. Not a period |
| "Bakıdan Atlantidaya" | `unknown_destination`. A word counts as a place only with a capital letter or right next to the city that was understood, and never when it is an everyday word ("uçuşa", "sonra", "axşama"...) |

What is not sure is asked about, never guessed: a child or an infant without an adult is `missing_adult`; a return word without a day is
`missing_return_date`. More than nine travellers is `bad_passengers`.

Only when the rules find no route at all, and `TravelAssistant` (Gemini) is set up, the assistant is asked to read the sentence. Its
answer is checked against the real airports, today's date and the allowed values before any of it is used. If the assistant is not set up,
is down, or answers badly, the rules' answer stands, so voice and typed search keep working without any AI key. The assistant fills the
route, the day, the part of the day, the cabin, the passengers and "cheapest" only; it does not fill the fields above.
`VoiceSearch:UseAssistantForComplexRequests=false` switches this off.

### `POST /api/flights/search-range`

The real bookable flights of one route over a period, in one of three orders: `sortBy` is `"price"` (cheapest first, the default),
`"departure"` (earliest first) or `"duration"` (the shortest flight first). Optionally one part of the day (`timeOfDay`), one cabin and one
kind of flight:

| Field | Meaning |
| --- | --- |
| `stops` | `"direct"` keeps only flights without a stop; `"connecting"` keeps only flights with a stop and journeys of two flights; left out, both are returned. Anything else is `400` |
| `lowestOnly` | only the flights and journeys at the lowest price of the whole period (every one that ties) |

Same rules as the ordinary search (flights that have left, flights without enough free seats, and flights without the cabin are left out).
At most `VoiceSearch:MaxRangeDays` (62) days and `VoiceSearch:MaxResults` (50) results.

**The whole period is compared before the list is cut.** The order (and so "the shortest" or "the cheapest") is that of every matching flight
and journey of the period, not of the first 50. `totalFound` is the true count; `lowestPriceCount` says how many share the lowest price.
Equal values are ordered by the next one (price, departure) and then by flight id, so the same one is always chosen.

**Journeys with a transfer.** Besides `flights`, the answer has `connections`: journeys of two flights through one transfer airport, found
by exactly the rules a one-day search and booking apply (a hub that active routes join to both ends; the second flight leaves 90 minutes to
8 hours after the first lands; both flights sell the cabin and have free seats for everyone; the fare is the sum of the two). A period holds
every journey of its days (the cap of a one-day search, `ConnectionRules.MaxConnectionsPerSearch`, does not apply to a period, whose size is
bounded by `MaxRangeDays` and `MaxResults`). Flights and journeys are ordered together, and the cut at `MaxResults` is made on the
combined list, so `flights` and `connections` together hold at most `MaxResults`. With `stops: "direct"` there are no journeys. A flight
that has one flight number but stops on the way (its `stops` is above 0) is in `flights`, not in `connections`.

Parts of the day, at the origin airport: morning 05:00-11:59, afternoon 12:00-17:59, evening 18:00-23:59, night 00:00-04:59. For a journey the
part of the day is that of its first flight.

## The results page

The home page hands the criteria to `flight-results.html` in the address:

| Parameter | From | Effect |
| --- | --- | --- |
| `departureDate` | `date` | one day: the normal search (its flights and its journeys) |
| `dateFrom`, `dateTo`, `defaultPeriod` | `dateFrom`, `dateTo` | a period: `search-range` |
| `cheapest`, `single` | `cheapest`, `cheapestSingle` | the lowest price of the period |
| `fastest=1` | `fastest` | a period is asked for by `sortBy: "duration"`; on one day the list is sorted "Ən sürətli" |
| `stops=direct` / `connecting` | `stops` | a period: sent to the API; on one day the "Dayanacaqlar" filter is set (direct, or one stop and more) and can be changed |
| `tripType=roundtrip`, `returnDate` | `returnDate` | a round trip: the outbound flight is chosen first, then the return flight on the return day |
| `adults`, `children`, `infants`, `passengers` | `adults`, `children`, `infants` | `passengers` is the seats (`adults + children`); an infant has no seat |

## Settings (`VoiceSearch`, not secret)

| Setting | Default | Meaning |
| --- | --- | --- |
| `DefaultRangeDays` | `30` | Days searched for the cheapest flight, or when no day was said (today included) |
| `MaxRangeDays` | `62` | Widest period of one search |
| `MaxResults` | `50` | Most flights and journeys returned by a period search |
| `MaxTextLength` | `300` | Longest sentence that is read |
| `UseAssistantForComplexRequests` | `true` | Let Gemini read sentences the rules cannot (needs `TravelAssistant:ApiKey`) |

No key is needed for voice search. The Gemini key stays on the server (user-secrets or environment), as for Ask AEROVA.

## What the browser does, and what is tested

The microphone, the permission and the recognition of speech are the browser's (Web Speech API) and cannot be tested from the server: the
tests of this repository start from the recognised text. The interpreter, the period search and the endpoints are covered by
`Service.Tests/VoiceSearchTests.cs`, `Service.Tests/VoiceSearchAzerbaijaniTests.cs`, `AirlineReservationSystem.Tests/VoiceSearchEndpointTests.cs`
and `AirlineReservationSystem.Tests/ConnectionJourneyTests.cs`.
