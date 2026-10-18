using Domain.Entities;
using Service.Services;

namespace Service.Tests;

public class AirportServiceTests
{
    private readonly TestData _data = new();

    [Fact]
    public async Task ActiveAirports_IncludeTheirTimeZone()
    {
        var closed = new Airport { Id = 9, Code = "NAJ", Name = "Nakhchivan", City = "Nakhchivan", CountryCode = "AZ", TimeZoneId = "Asia/Baku", IsActive = false };
        var service = new AirportService(new FakeAirportRepository(_data.Baku, _data.London, closed));

        var airports = await service.GetActiveAsync();

        Assert.Equal(new[] { "GYD", "LHR" }, airports.Select(a => a.Code));
        Assert.Equal(new[] { "Asia/Baku", "Europe/London" }, airports.Select(a => a.TimeZoneId));
    }

    [Fact]
    public void TimeZones_AreValidIanaIds()
    {
        // Etc/UTC is what the AddAirportTimeZone migration gives airports that existed before it.
        foreach (var zone in new[] { _data.Baku.TimeZoneId, _data.London.TimeZoneId, _data.Istanbul.TimeZoneId, "Etc/UTC" })
        {
            Assert.True(TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _), $"{zone} is not a known time zone");
        }
    }
}
