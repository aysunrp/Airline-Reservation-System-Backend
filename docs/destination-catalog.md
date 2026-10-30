# Destination catalog

One database-backed catalog says where AEROVA may fly: 20 countries, 37 cities and 39 airports (Istanbul has two: IST and SAW). Explore Destinations (home and destinations pages),
the search suggestions, the voice search and the admin panel all read it. It says nothing about flights: a destination without a route is shown as
"No scheduled flights yet" and searches for it return an empty list. Nothing in the catalog creates a route, schedule, fare or flight.

## Model (migration `AddDestinationCatalog`)

| Table | Key and constraints |
|---|---|
| `Countries` | `Code` (ISO 3166-1 alpha-2) unique; `Name`, `NameAz`, `NameRu`; `Description`, `DescriptionAz`, `DescriptionRu`; `ImageFile`; `SortOrder`; `IsActive` |
| `DestinationCities` | `(CountryId, Name)` unique; `NameAz`, `NameRu`; `Aliases` (extra spellings, `;`-separated); `IsHub` (the home base, Baku); `ImageFile` and `Description`, `DescriptionAz`, `DescriptionRu` (the city's own photograph and short description); foreign key to `Countries`, `ON DELETE NO ACTION` |
| `Airports` (existing) | new nullable `CityId`, foreign key to `DestinationCities`, `ON DELETE NO ACTION`. The existing `City` and `CountryCode` text columns stay, so nothing that reads airports changes. `Code` stays unique. |

The migrations only add tables and columns (`AddDestinationCatalog`, `AddDestinationCityHub`, `AddCityDetails`). They change and delete no existing row.

## Seed

`DestinationCatalogData` (Service/Helpers/Seeding) holds the catalog; `DestinationCatalogSeeder` adds it. A country is found by its code, a city by
(country, name), an airport by its IATA code. What exists is never overwritten: an airport that already exists (GYD, LHR, CDG, DXB, IST, TBS in the
demo network) keeps its own data and is only linked to its city. Running it again adds nothing. It is reference data, separate from `DemoSeedData`.

It runs when the API starts (`DestinationCatalog:SeedOnStartup`, true by default) but only once the migrations are applied; otherwise it logs what to run
and skips. A database that cannot be reached does not stop the API from starting. Set `DestinationCatalog:SeedOnStartup` to `false` to turn it off.

Every IATA code was checked against the infobox of the airport's English Wikipedia article (38 of 38 matched).

## Deployment (nothing is applied by the code)

1. Back up the database.
2. `dotnet ef database update --project Repository --startup-project AirlineReservationSystem` (adds `Countries`, `DestinationCities`, `Airports.CityId`).
3. Start the API: the log says how many countries, cities and airports were added. Start it again: all counts are 0.
4. Apply the migration **before** the new backend serves traffic. Without the new `Airports.CityId` column the airport list (search suggestions, voice search) and `GET /api/destinations` answer the 422 `database_setup_required` error, because the code already reads that column.

## Seeding by hand (if the API did not do it)

`docs/sql/seed-destination-catalog.sql` is the same seed as T-SQL: idempotent, one transaction, never overwrites, only links existing airports. It was generated from `DestinationCatalogData`. Run it after the migration:

```
sqlcmd -S .\SQLEXPRESS -E -d AerovaDb -b -f 65001 -i docs\sql\seed-destination-catalog.sql
```

It prints the totals (20 countries, 37 cities) at the end; running it again changes nothing.

## API

The hierarchy is Country → City → Airport → Flight (a flight's route joins two airports). Every page reads it from here.

| Endpoint | Who | What |
|---|---|---|
| `GET /api/destinations` | everyone | Active countries with active cities (each with its photograph and description) and airports; `hasRoutes` per airport |
| `GET /api/destinations/cities/{id}` | everyone | One city's page: the city with **its own** airports, its country, and the **other active cities of the same country** (each with photograph, description, airports). 404 for the home base (Baku), a switched-off city or country, an unknown id |
| `GET /api/destinations/cities/{id}/flights` | everyone | The upcoming, not cancelled flights from Baku (the hub) to the airports of that city, earliest first, with the lowest stored base fare. Empty when none is scheduled: nothing is borrowed from another city |
| `GET /api/admin/destinations` | Admin, SuperAdmin | The same catalog with switched-off records |
| `PUT /api/admin/destinations/countries/{id}` and `.../cities/{id}` | Admin, SuperAdmin | Edit names and descriptions (the city's English name is its key and stays) |
| `PUT /api/admin/destinations/countries/{id}/active` and `.../cities/{id}/active` | Admin, SuperAdmin | Switch a country or city on or off |

The catalog is loaded once per request (countries with cities and airports in one query, the route set in another), so a city page costs three queries and the flight list one.
Countries and cities are switched off, never deleted: a switched-off one leaves the public catalog while its airports, routes, flights and bookings stay.
The database refuses to delete a country that has cities or a city that has airports. Airports are managed (and refused deletion when a route, schedule
stop or flight segment uses them) under `api/admin/airports`.

## Voice search

`VoiceSearchInterpreter` reads, besides its built-in aliases, the English, Azerbaijani and Russian name and the `Aliases` of the city record of every active
airport. Cities with two airports (Tokyo: HND, NRT) are not guessed: the answer asks which airport. Russian declined forms (Осаки, Барселону) are stored
as aliases. A city with a new spelling needs only an alias on its record.

## Limits

- Only the 37 cities are in the catalog; an airport created in the admin panel for another city is searchable but not part of Explore Destinations.
- Descriptions are one sentence per country, kept in the database in the three languages. Photograph credits: `destination-images.md`.
- No flights exist for the new airports until routes and schedules are created for them on purpose (admin panel or a deliberate seed).
