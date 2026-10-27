namespace Service.Helpers.Exceptions;

/// <summary>Thrown when sign-in is paused after repeated failures; becomes 429 with a Retry-After header.</summary>
public class TooManyAttemptsException : Exception
{
    public TooManyAttemptsException(string message, TimeSpan retryAfter) : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}
