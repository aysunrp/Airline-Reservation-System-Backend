using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Payment;

namespace Service.Helpers.Validators;

public class PaymentRequestValidator : AbstractValidator<PaymentRequestDto>
{
    public PaymentRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Pnr).NotEmpty().Matches("^[A-Za-z0-9]{6}$").WithMessage("Enter your booking reference.");
        RuleFor(x => x.Method).IsInEnum();

        When(x => x.Method == PaymentMethod.Card, () =>
        {
            RuleFor(x => x.CardholderName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.CardNumber).NotEmpty()
                .Must(n => IsValidCardNumber(Digits(n)))
                .WithMessage("Enter a valid card number.");
            RuleFor(x => x.ExpiryMonth).NotNull().InclusiveBetween(1, 12);
            RuleFor(x => x.ExpiryYear).NotNull();
            RuleFor(x => x)
                .Must(x => x.ExpiryMonth is null || x.ExpiryYear is null || !IsExpired(x.ExpiryMonth.Value, x.ExpiryYear.Value, timeProvider))
                .WithName("Expiry")
                .WithMessage("The card has expired.");
            RuleFor(x => x.Cvv).NotEmpty().Matches(@"^\d{3,4}$").WithMessage("Enter a valid CVV.");
        });
    }

    internal static string Digits(string? cardNumber)
        => new((cardNumber ?? string.Empty).Where(c => c != ' ' && c != '-').ToArray());

    private static bool IsExpired(int month, int year, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return year < now.Year || (year == now.Year && month < now.Month);
    }

    // 13–19 digits passing the Luhn checksum.
    private static bool IsValidCardNumber(string digits)
    {
        if (digits.Length is < 13 or > 19 || !digits.All(char.IsDigit))
        {
            return false;
        }

        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }
}
