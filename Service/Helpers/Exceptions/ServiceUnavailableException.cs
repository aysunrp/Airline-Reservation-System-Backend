namespace Service.Helpers.Exceptions;

/// <summary>Thrown when something the request needs (such as e-mail sending) is not available; becomes 503.</summary>
public class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message) : base(message)
    {
    }
}
