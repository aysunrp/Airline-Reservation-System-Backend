using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Tests;

public class TravelDocumentPolicyTests
{
    private static readonly Airport Baku = new() { Id = 1, Code = "GYD", CountryCode = "AZ" };
    private static readonly Airport Nakhchivan = new() { Id = 2, Code = "NAJ", CountryCode = "AZ" };
    private static readonly Airport Istanbul = new() { Id = 3, Code = "IST", CountryCode = "TR" };
    private static readonly Airport London = new() { Id = 4, Code = "LHR", CountryCode = "GB" };
    private static readonly Airport Dubai = new() { Id = 5, Code = "DXB", CountryCode = "AE" };
    private static readonly Airport Tbilisi = new() { Id = 6, Code = "TBS", CountryCode = "GE" };

    private readonly TravelDocumentPolicy _policy = new(Options.Create(TestData.FrontendDocumentRules()));

    /// <summary>A flight through the given airports, one segment per hop.</summary>
    private static Flight Flight(params Airport[] airports)
    {
        var flight = new Flight { Route = new Route { Origin = airports[0], Destination = airports[^1] } };
        for (var i = 1; i < airports.Length; i++)
        {
            flight.Segments.Add(new FlightSegment { SequenceNo = i, Origin = airports[i - 1], Destination = airports[i] });
        }

        return flight;
    }

    private static BookingPassengerRequestDto Passenger(string nationality, DocumentType document, DateOnly expiry) => new()
    {
        Title = "Ms", FirstName = "Aysun", LastName = "Panahli", NationalityCode = nationality,
        DocumentType = document, DocumentIssuingCountry = "AZ", DocumentExpiryDate = expiry
    };

    [Theory]
    [InlineData("AZ", new[] { DocumentType.NationalIdCard, DocumentType.Passport })]
    [InlineData("TR", new[] { DocumentType.Passport })]
    public void Domestic_AzerbaijaniCitizensMayUseTheirIdCard(string nationality, DocumentType[] expected)
    {
        var requirement = _policy.GetRequirement(new[] { Flight(Baku, Nakhchivan) }, nationality);

        Assert.Equal(expected, requirement.Documents);
        Assert.Equal(0, requirement.MinValidityDays);
    }

    [Theory]
    [InlineData("GB")]
    [InlineData("FR")]
    [InlineData("TR")]
    [InlineData("AE")]
    [InlineData("GE")]
    public void EveryFlightOutOfAzerbaijan_NeedsAPassport_AndNeverOffersTheIdCard(string destinationCountry)
    {
        var destination = new Airport { Id = 50, Code = "XXX", CountryCode = destinationCountry };

        Assert.Equal(new[] { DocumentType.Passport }, _policy.GetRequirement(new[] { Flight(Baku, destination) }, "AZ").Documents);
        Assert.Equal(new[] { DocumentType.Passport }, _policy.GetRequirement(new[] { Flight(Baku, destination) }, "az").Documents);
    }

    [Fact]
    public void ADomesticFlightIsDecidedByTheAirportsCountries_NotByTheirNames()
    {
        // A foreign airport that carries a Baku-sounding name is still foreign; an Azerbaijani airport is domestic whatever it is called.
        var disguised = new Airport { Id = 60, Code = "LHR", City = "Baku", Name = "Baku", CountryCode = "GB" };
        var ganja = new Airport { Id = 61, Code = "GNJ", City = "Ganja", CountryCode = "AZ" };

        Assert.Equal(new[] { DocumentType.Passport }, _policy.GetRequirement(new[] { Flight(Baku, disguised) }, "AZ").Documents);
        Assert.Equal(new[] { DocumentType.NationalIdCard, DocumentType.Passport }, _policy.GetRequirement(new[] { Flight(Baku, ganja) }, "AZ").Documents);
        Assert.Equal(new[] { DocumentType.NationalIdCard, DocumentType.Passport }, _policy.GetRequirement(new[] { Flight(ganja, Baku) }, "AZ").Documents);
    }

    [Fact]
    public void Check_JudgesEveryPassengerOnTheirOwn()
    {
        var lastDay = new DateOnly(2026, 11, 10);
        var withIdCard = Passenger("AZ", DocumentType.NationalIdCard, new DateOnly(2030, 1, 1));
        withIdCard.FirstName = "Nigar";
        var withPassport = Passenger("AZ", DocumentType.Passport, new DateOnly(2030, 1, 1));

        // Domestic: each may use their own document.
        _policy.Check(new[] { Flight(Baku, Nakhchivan) }, new[] { withPassport, withIdCard }, lastDay);

        // International: the second passenger's ID card is refused, and the message names that passenger.
        var refused = Assert.Throws<BusinessRuleException>(() => _policy.Check(new[] { Flight(Baku, London) }, new[] { withPassport, withIdCard }, lastDay));
        Assert.Contains("Nigar", refused.Message);
        Assert.DoesNotContain("Aysun", refused.Message);
    }

    [Fact]
    public void Check_RequiresTheExpiryDate_AndAnAzerbaijaniIssuedIdCard_WithoutRevealingNumbers()
    {
        var lastDay = new DateOnly(2026, 11, 10);
        var noExpiry = Passenger("AZ", DocumentType.Passport, lastDay);
        noExpiry.DocumentExpiryDate = null;
        noExpiry.DocumentNumber = "C01234567";
        var foreignIdCard = Passenger("AZ", DocumentType.NationalIdCard, new DateOnly(2030, 1, 1));
        foreignIdCard.DocumentIssuingCountry = "TR";
        foreignIdCard.DocumentNumber = "AA1234567";

        var missing = Assert.Throws<BusinessRuleException>(() => _policy.Check(new[] { Flight(Baku, London) }, new[] { noExpiry }, lastDay));
        var wrongIssuer = Assert.Throws<BusinessRuleException>(() => _policy.Check(new[] { Flight(Baku, Nakhchivan) }, new[] { foreignIdCard }, lastDay));

        Assert.DoesNotContain("C01234567", missing.Message);
        Assert.DoesNotContain("AA1234567", wrongIssuer.Message);
    }

    [Fact]
    public void Dubai_NeedsSixMonthsOfValidity_AndCountriesWithoutRulesNeedAPassport()
    {
        Assert.Equal(180, _policy.GetRequirement(new[] { Flight(Baku, Dubai) }, "AZ").MinValidityDays);
        Assert.Equal(new[] { DocumentType.Passport }, _policy.GetRequirement(new[] { Flight(Baku, Tbilisi) }, "AZ").Documents);
    }

    [Fact]
    public void ATripCombinesItsFlights()
    {
        // Out to Istanbul and on to Dubai (180 days): the whole trip needs a passport valid 180 days.
        var requirement = _policy.GetRequirement(new[] { Flight(Baku, Istanbul), Flight(Istanbul, Dubai) }, "AZ");

        Assert.Equal(new[] { DocumentType.Passport }, requirement.Documents);
        Assert.Equal(180, requirement.MinValidityDays);
    }

    [Fact]
    public void Check_RejectsTheDocumentType_AndTooShortValidity()
    {
        var lastDay = new DateOnly(2026, 11, 10);

        _policy.Check(new[] { Flight(Baku, Nakhchivan) }, new[] { Passenger("AZ", DocumentType.NationalIdCard, lastDay) }, lastDay);

        var idCard = Assert.Throws<BusinessRuleException>(() =>
            _policy.Check(new[] { Flight(Baku, London) }, new[] { Passenger("AZ", DocumentType.NationalIdCard, new DateOnly(2030, 1, 1)) }, lastDay));
        Assert.Contains("pasport təqdim edin", idCard.Message);

        _policy.Check(new[] { Flight(Baku, Dubai) }, new[] { Passenger("AZ", DocumentType.Passport, lastDay.AddDays(180)) }, lastDay);
        var shortPassport = Assert.Throws<BusinessRuleException>(() =>
            _policy.Check(new[] { Flight(Baku, Dubai) }, new[] { Passenger("AZ", DocumentType.Passport, lastDay.AddDays(179)) }, lastDay));
        Assert.Contains("səyahətdən 180 gün sonra", shortPassport.Message);

        Assert.Throws<BusinessRuleException>(() =>
            _policy.Check(new[] { Flight(Baku, London) }, new[] { Passenger("AZ", DocumentType.Passport, lastDay.AddDays(-1)) }, lastDay));
    }

    [Fact]
    public void WithoutAnyConfiguration_APassportIsRequiredEverywhere()
    {
        var empty = new TravelDocumentPolicy(Options.Create(new TravelDocumentSettings()));

        Assert.Equal(new[] { DocumentType.Passport }, empty.GetRequirement(new[] { Flight(Baku, Nakhchivan) }, "AZ").Documents);
        Assert.Equal(new[] { DocumentType.Passport }, empty.GetRequirement(new[] { Flight(Baku, Istanbul) }, "AZ").Documents);
    }
}
