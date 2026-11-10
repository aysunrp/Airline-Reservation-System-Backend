-- Repairs the text written by seed-destination-catalog.sql when it was run without the UTF-8 code page
-- (sqlcmd -f 65001 was missing, so every non-ASCII letter was stored as two or three wrong characters).
-- It only touches rows that carry those wrong characters; correct text is left alone. Run it once, then run the seed again:
--   sqlcmd -S ".\SQLEXPRESS" -E -C -d AerovaDb -b -f 65001 -i repair-city-text-encoding.sql
--   sqlcmd -S ".\SQLEXPRESS" -E -C -d AerovaDb -b -f 65001 -i seed-destination-catalog.sql
-- The script is plain ASCII on purpose: it does not depend on the code page.
-- The wrong characters are looked up by code (195 A-tilde, 201 E-acute, 208 and 209 Cyrillic lead bytes, 8482 trade mark sign).
SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRAN;

-- 1. The seed could not find "Male" (with the accent) by name and added a second, mis-spelled city without airports; remove only that copy.
DELETE c FROM DestinationCities c
WHERE c.Name COLLATE Latin1_General_100_BIN2 LIKE N'Mal' + NCHAR(195) + N'%'
  AND NOT EXISTS (SELECT 1 FROM Airports a WHERE a.CityId = c.Id)
  AND EXISTS (SELECT 1 FROM DestinationCities o WHERE o.CountryId = c.CountryId AND o.Id <> c.Id AND o.Name COLLATE Latin1_General_100_BIN2 = N'Mal' + NCHAR(233));

-- 2. Empty the mis-encoded descriptions; the seed fills a description only while it is empty, and it writes the right text.
UPDATE DestinationCities SET Description = N''
WHERE Description COLLATE Latin1_General_100_BIN2 LIKE N'%' + NCHAR(195) + N'%' OR Description COLLATE Latin1_General_100_BIN2 LIKE N'%' + NCHAR(8482) + N'%';
UPDATE DestinationCities SET DescriptionAz = N''
WHERE DescriptionAz COLLATE Latin1_General_100_BIN2 LIKE N'%' + NCHAR(195) + N'%' OR DescriptionAz COLLATE Latin1_General_100_BIN2 LIKE N'%' + NCHAR(201) + N'%'
   OR DescriptionAz COLLATE Latin1_General_100_BIN2 LIKE N'%' + NCHAR(8482) + N'%';
UPDATE DestinationCities SET DescriptionRu = N''
WHERE DescriptionRu COLLATE Latin1_General_100_BIN2 LIKE N'%' + NCHAR(208) + N'%' OR DescriptionRu COLLATE Latin1_General_100_BIN2 LIKE N'%' + NCHAR(209) + N'%';

-- 3. Sabiha Gokcen (o with diaeresis 246, c with cedilla 231).
UPDATE Airports SET Name = N'Sabiha G' + NCHAR(246) + N'k' + NCHAR(231) + N'en International Airport'
WHERE Code = N'SAW' AND Name COLLATE Latin1_General_100_BIN2 LIKE N'%' + NCHAR(195) + N'%';

COMMIT;
SELECT (SELECT COUNT(*) FROM DestinationCities) AS Cities,
       (SELECT COUNT(*) FROM DestinationCities WHERE Description = N'' OR DescriptionAz = N'' OR DescriptionRu = N'') AS CitiesToRefill;
