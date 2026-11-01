-- Marks Baku (AEROVA's home base) as the hub of the destination catalog, once the AddDestinationCityHub migration is applied.
-- Baku stays in the catalog, the airports and the routes; it is only no longer offered as a place to travel to. Safe to run again.
UPDATE DestinationCities SET IsHub = 1
WHERE Name = N'Baku' AND CountryId = (SELECT Id FROM Countries WHERE Code = N'AZ') AND IsHub = 0;
SELECT Name, IsHub FROM DestinationCities WHERE IsHub = 1;
