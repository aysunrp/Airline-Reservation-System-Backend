namespace Service.Helpers.Exceptions;

/// <summary>Thrown when a request conflicts with existing data (e.g. a duplicate email or an already taken seat).</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
