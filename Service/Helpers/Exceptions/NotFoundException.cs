namespace Service.Helpers.Exceptions;

/// <summary>Thrown when a requested resource does not exist or does not belong to the caller.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
