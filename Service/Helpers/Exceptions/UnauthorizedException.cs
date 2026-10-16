namespace Service.Helpers.Exceptions;

/// <summary>Thrown when credentials are wrong or the account cannot sign in.</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
