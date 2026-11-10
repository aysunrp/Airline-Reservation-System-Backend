using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class TravelDocumentPolicy : ITravelDocumentPolicy
{
    private static readonly IReadOnlyList<DocumentType> PassportOnly = new[] { DocumentType.Passport };

    private readonly TravelDocumentSettings _settings;

    public TravelDocumentPolicy(IOptions<TravelDocumentSettings> settings)
    {
        _settings = settings.Value;
    }

    public TravelDocumentRequirement GetRequirement(IReadOnlyList<Flight> flights, string nationalityCode)
    {
        var nationality = nationalityCode.Trim().ToUpperInvariant();
        IEnumerable<DocumentType>? documents = null;
        var minValidityDays = 0;

        foreach (var flight in flights)
        {
            var (flightDocuments, flightDays) = ForFlight(Countries(flight), nationality);
            documents = documents is null ? flightDocuments : documents.Intersect(flightDocuments);
            minValidityDays = Math.Max(minValidityDays, flightDays);
        }

        var accepted = documents?.Distinct().ToList() ?? new List<DocumentType>();
        return new TravelDocumentRequirement(accepted.Count > 0 ? accepted : PassportOnly, minValidityDays);
    }

    public void Check(IReadOnlyList<Flight> flights, IReadOnlyList<BookingPassengerRequestDto> passengers, DateOnly lastTravelDate)
    {
        foreach (var passenger in passengers)
        {
            var name = $"{passenger.FirstName} {passenger.LastName}";
            var requirement = GetRequirement(flights, passenger.NationalityCode);

            if (!requirement.Documents.Contains(passenger.DocumentType))
            {
                throw new BusinessRuleException(
                    $"{name} üçün bu səyahətdə {Describe(passenger.DocumentType)} qəbul edilmir. {string.Join(" və ya ", requirement.Documents.Select(Describe))} təqdim edin.");
            }

            if (passenger.DocumentType == DocumentType.NationalIdCard
                && !string.Equals(passenger.DocumentIssuingCountry?.Trim(), "AZ", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException($"{name} üçün şəxsiyyət vəsiqəsi yalnız Azərbaycan tərəfindən verilmiş ola bilər.");
            }

            if (passenger.DocumentExpiryDate is not DateOnly expiry)
            {
                throw new BusinessRuleException($"{name} üçün sənədin etibarlılıq müddətini daxil edin.");
            }

            var validUntil = lastTravelDate.AddDays(requirement.MinValidityDays);
            if (expiry < validUntil)
            {
                throw new BusinessRuleException(requirement.MinValidityDays == 0
                    ? $"{name} adlı sərnişinin səyahət sənədinin müddəti səyahət bitməmiş başa çatır."
                    : $"{name} adlı sərnişinin səyahət sənədi ən azı {validUntil:dd.MM.yyyy} tarixinədək (səyahətdən {requirement.MinValidityDays} gün sonra) etibarlı olmalıdır.");
            }
        }
    }

    // Departure country first, then the country of every stop and of the destination.
    private static List<string> Countries(Flight flight)
    {
        var segments = flight.Segments.OrderBy(s => s.SequenceNo).ToList();
        if (segments.Count == 0)
        {
            return new List<string> { flight.Route.Origin.CountryCode.ToUpperInvariant(), flight.Route.Destination.CountryCode.ToUpperInvariant() };
        }

        var countries = new List<string> { segments[0].Origin.CountryCode };
        countries.AddRange(segments.Select(s => s.Destination.CountryCode));
        return countries.Select(c => c.ToUpperInvariant()).ToList();
    }

    private (IEnumerable<DocumentType> Documents, int MinValidityDays) ForFlight(List<string> countries, string nationality)
    {
        if (countries.Count >= 2 && countries.All(c => !string.IsNullOrEmpty(c) && c == countries[0]))
        {
            var domestic = Find(_settings.Domestic, countries[0]);
            return (Accepted(domestic, nationality) ?? PassportOnly, domestic?.MinValidityDays ?? 0);
        }

        IEnumerable<DocumentType>? documents = null;
        var minValidityDays = _settings.InternationalDefault.MinValidityDays;
        foreach (var country in countries.Skip(1))
        {
            var rule = Find(_settings.International, country);
            var accepted = Accepted(rule, nationality) ?? NonEmpty(_settings.InternationalDefault.Documents) ?? PassportOnly;
            documents = documents is null ? accepted : documents.Intersect(accepted);
            minValidityDays = Math.Max(minValidityDays, rule?.MinValidityDays ?? 0);
        }

        var result = documents?.ToList();
        return (result is { Count: > 0 } ? result : PassportOnly, minValidityDays);
    }

    private static CountryDocumentRule? Find(Dictionary<string, CountryDocumentRule> rules, string country)
        => rules.FirstOrDefault(r => string.Equals(r.Key, country, StringComparison.OrdinalIgnoreCase)).Value;

    private static IReadOnlyList<DocumentType>? Accepted(CountryDocumentRule? rule, string nationality)
    {
        if (rule is null)
        {
            return null;
        }

        var byNationality = rule.ByNationality.FirstOrDefault(r => string.Equals(r.Key, nationality, StringComparison.OrdinalIgnoreCase)).Value;
        return NonEmpty(byNationality) ?? NonEmpty(rule.Documents);
    }

    private static IReadOnlyList<DocumentType>? NonEmpty(List<DocumentType>? documents) => documents is { Count: > 0 } ? documents : null;

    private static string Describe(DocumentType type) => type == DocumentType.NationalIdCard ? "şəxsiyyət vəsiqəsi" : "pasport";
}
