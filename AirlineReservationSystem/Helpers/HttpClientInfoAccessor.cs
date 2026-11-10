using Service.Services.Interfaces;

namespace AirlineReservationSystem.Helpers;

/// <summary>Reads the browser description and the network address of the current request (empty outside a request).</summary>
public sealed class HttpClientInfoAccessor : IClientInfoAccessor
{
    private readonly IHttpContextAccessor _http;

    public HttpClientInfoAccessor(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string? UserAgent
    {
        get
        {
            var value = _http.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public string? IpAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
