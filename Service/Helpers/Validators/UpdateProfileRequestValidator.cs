using Domain.Constants;
using FluentValidation;
using Service.Helpers.DTOs.Account;

namespace Service.Helpers.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequestDto>
{
    public UpdateProfileRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Title)
            .Must(t => t is null || PersonTitles.IsValid(t))
            .WithMessage($"Title must be {PersonTitles.Description}.");
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
    }
}
