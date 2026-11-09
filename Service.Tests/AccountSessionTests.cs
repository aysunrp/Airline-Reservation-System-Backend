using Service.Services;

namespace Service.Tests;

/// <summary>What a session shows about a device: enough to recognise it, nothing that identifies it.</summary>
public class AccountSessionTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36", "Chrome, Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0", "Edge, Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", "Safari, iOS")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0", "Firefox, Linux")]
    [InlineData("curl/8.0", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TheDevice_IsDescribedByBrowserAndSystem(string? userAgent, string? expected)
        => Assert.Equal(expected, AccountSessionService.DescribeDevice(userAgent));

    [Theory]
    [InlineData("203.0.113.57", "203.0.113.x")]
    [InlineData("::ffff:203.0.113.57", "203.0.113.x")]
    [InlineData("2001:db8:85a3::8a2e:370:7334", "2001:db8:85a3::x")]
    [InlineData("::1", "localhost")]
    [InlineData("127.0.0.1", "localhost")]
    [InlineData("not an address", null)]
    [InlineData(null, null)]
    public void TheAddress_HasItsLastPartHidden(string? address, string? expected)
        => Assert.Equal(expected, AccountSessionService.MaskIp(address));

    [Theory]
    [InlineData("aysun@example.com", "a***@example.com")]
    [InlineData("@example.com", "***")]
    [InlineData("nobody", "***")]
    public void TheNewAddress_IsShownMasked(string email, string expected)
        => Assert.Equal(expected, AccountSettingsService.MaskEmail(email));
}
