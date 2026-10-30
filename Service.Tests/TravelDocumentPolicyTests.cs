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
        DocumentType = document, DocumentExpiryDate = expiry
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

    [Fact]
    public void DirectToTurkey_AzerbaijaniIdCardIsAccepted_ButNotViaTurkeyToLondon()
    {
        Assert.Contains(DocumentType.NationalIdCard, _policy.GetRequirement(new[] { Flight(Baku, Istanbul) }, "az").Documents);

        // Every country after the departure one must accept the document: Turkey would, Britain does not.
        Assert.Equal(new[] { DocumentType.Passport }, _policy.GetRequirement(new[] { Flight(Baku, Istanbul, London) }, "AZ").Documents);
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
        // Out to Istanbul (ID card fine) and on to Dubai (passport, 180 days): the whole trip needs a passport valid 180 days.
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
