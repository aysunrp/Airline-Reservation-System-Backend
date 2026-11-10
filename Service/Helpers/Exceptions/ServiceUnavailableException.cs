namespace Service.Helpers.Exceptions;

/// <summary>Thrown when something the request needs (such as e-mail sending) is not available; becomes 503.</summary>
public class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message) : base(message)
    {
    }

    /// <summary>A short machine-readable reason (for example "assistant_key_rejected") that travels with the 503 as its "code"; null when there is none.</summary>
    public ServiceUnavailableException(string message, string? code) : base(message)
    {
        Code = code;
    }

    public string? Code { get; }
}
