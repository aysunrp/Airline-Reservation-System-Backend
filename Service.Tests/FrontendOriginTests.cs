using Service.Helpers;

namespace Service.Tests;

public class FrontendOriginTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5500", "http://localhost:5500", true)]
    [InlineData("http://localhost:5500", "http://localhost:5500", true)]
    [InlineData("https://evil.example", "http://localhost:5500", false)]
    [InlineData("http://localhost:5501", "http://localhost:5500", false)]
    [InlineData("http://127.0.0.1:5500/Airline-Reservation-System-Frontend", "http://localhost:5500", true)]
    [InlineData("http://localhost:5500/Frontend?next=https://evil.example", "http://localhost:5500", false)]
    [InlineData("http://localhost:5500/Frontend#x", "http://localhost:5500", false)]
    [InlineData("http://user:secret@localhost:5500", "http://localhost:5500", false)]
    public void IsAllowed_respects_configured_frontend(string candidate, string configured, bool allowed)
    {
        Assert.Equal(allowed, FrontendOrigin.IsAllowed(candidate, configured));
    }

    [Fact]
    public void ResolveBaseUrl_uses_return_origin_when_allowed()
    {
        var resolved = FrontendOrigin.ResolveBaseUrl("http://127.0.0.1:5500", "http://localhost:5500");
        Assert.Equal("http://127.0.0.1:5500", resolved);
    }

    [Fact]
    public void ResolveBaseUrl_keeps_the_folder_the_site_is_served_from()
    {
        var resolved = FrontendOrigin.ResolveBaseUrl("http://127.0.0.1:5500/Airline-Reservation-System-Frontend/", "http://localhost:5500");
        Assert.Equal("http://127.0.0.1:5500/Airline-Reservation-System-Frontend", resolved);
        Assert.Equal("http://127.0.0.1:5500/Airline-Reservation-System-Frontend/payment-success.html", resolved + "/payment-success.html");
    }

    [Fact]
    public void ResolveBaseUrl_falls_back_when_the_return_base_carries_a_query()
    {
        var resolved = FrontendOrigin.ResolveBaseUrl("http://localhost:5500/Frontend?x=1", "http://localhost:5500");
        Assert.Equal("http://localhost:5500", resolved);
    }

    [Fact]
    public void ResolveBaseUrl_falls_back_when_origin_not_allowed()
    {
        var resolved = FrontendOrigin.ResolveBaseUrl("https://attacker.test", "http://localhost:5500");
        Assert.Equal("http://localhost:5500", resolved);
    }
}
