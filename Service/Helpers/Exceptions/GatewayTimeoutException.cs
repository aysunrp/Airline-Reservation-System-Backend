namespace Service.Helpers.Exceptions;

/// <summary>Thrown when a service the request depends on (the AI provider) did not answer in time; becomes 504.</summary>
public class GatewayTimeoutException : Exception
{
    public GatewayTimeoutException(string message) : base(message)
    {
    }
}
