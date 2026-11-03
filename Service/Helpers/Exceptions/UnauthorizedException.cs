namespace Service.Helpers.Exceptions;

/// <summary>Thrown when credentials are wrong or the account cannot sign in.</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string message, string? code) : base(message)
    {
        Code = code;
    }

    /// <summary>A short machine-readable reason (for example email_not_verified) that the page can react to without reading the text.</summary>
    public string? Code { get; }
}
