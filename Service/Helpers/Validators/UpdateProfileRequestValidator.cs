using FluentValidation;
using Service.Helpers.DTOs.Account;

namespace Service.Helpers.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequestDto>
{
    private static bool IsDocumentNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '-'))
        {
            return false;
        }

        var length = DocumentNumbers.Normalize(value).Length;
        return length is >= 5 and <= 20;
    }

    public UpdateProfileRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DateOfBirth)
            .Must(d => d is null || d < DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.NationalityCode).Matches("^[A-Z]{2}$").When(x => x.NationalityCode is not null)
            .WithMessage("Nationality must be a two-letter country code.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).Matches(@"^\+?[0-9 ]{6,20}$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Enter a valid phone number.");
        RuleFor(x => x.FatherName).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.FatherName));
        RuleFor(x => x.FatherName).Must(name => System.Text.RegularExpressions.Regex.IsMatch(name!.Trim(), @"^[\p{L}][\p{L} .'\-]*$"))
            .When(x => !string.IsNullOrWhiteSpace(x.FatherName))
            .WithMessage("Enter the father's name with letters only.");
        // A document number is letters and digits (spaces and dashes are allowed and ignored): 5 to 20 of them.
        RuleFor(x => x.PassportNumber).Must(IsDocumentNumber).When(x => !string.IsNullOrWhiteSpace(x.PassportNumber))
            .WithMessage("Enter a valid passport number (5 to 20 letters and digits).");
        RuleFor(x => x.NationalIdNumber).Must(IsDocumentNumber).When(x => !string.IsNullOrWhiteSpace(x.NationalIdNumber))
            .WithMessage("Enter a valid identity card number (5 to 20 letters and digits).");
    }
}
