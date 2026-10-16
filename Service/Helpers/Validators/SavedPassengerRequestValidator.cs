using FluentValidation;
using Service.Helpers.DTOs.Passenger;

namespace Service.Helpers.Validators;

public class SavedPassengerRequestValidator : AbstractValidator<SavedPassengerRequestDto>
{
    public SavedPassengerRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Relationship).MaximumLength(30);
        RuleFor(x => x.DateOfBirth)
            .Must(d => d is null || d < DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.DocumentType).IsInEnum();
        RuleFor(x => x.NationalityCode).Matches("^[A-Z]{2}$").When(x => x.NationalityCode is not null)
            .WithMessage("Nationality must be a two-letter country code.");
        RuleFor(x => x.DocumentIssuingCountry).Matches("^[A-Z]{2}$").When(x => x.DocumentIssuingCountry is not null)
            .WithMessage("Issuing country must be a two-letter country code.");
        RuleFor(x => x.DocumentNumber).MaximumLength(30);

        // A document number only makes sense together with its type.
        RuleFor(x => x.DocumentType).NotNull().When(x => !string.IsNullOrEmpty(x.DocumentNumber))
            .WithMessage("Select the document type.");
    }
}
