namespace Service.Helpers.Exceptions;

/// <summary>Thrown when a request is valid in shape but breaks a business rule (e.g. an expired promo code).</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string message, string? code) : base(message)
    {
        Code = code;
    }

    /// <summary>A short machine-readable reason (for example link_expired) that the page can react to without reading the text.</summary>
    public string? Code { get; }
}
