namespace Service.Services.Interfaces;

/// <summary>What the current request says about the device (read from the HTTP request in the API layer; empty outside a request).</summary>
public interface IClientInfoAccessor
{
    string? UserAgent { get; }

    string? IpAddress { get; }
}
