namespace Service.Helpers.Exceptions;

/// <summary>Thrown when a request is valid in shape but breaks a business rule (e.g. an expired promo code).</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
