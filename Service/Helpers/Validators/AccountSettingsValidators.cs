using FluentValidation;
using Service.Helpers.DTOs.Account;

namespace Service.Helpers.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(100);
        // Same rule as registration and the password reset.
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6).MaximumLength(100);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword).WithMessage("The new password must be different from the current one.");
    }
}

public class RequestEmailChangeValidator : AbstractValidator<RequestEmailChangeDto>
{
    public RequestEmailChangeValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(100);
    }
}

public class ConfirmEmailChangeValidator : AbstractValidator<ConfirmEmailChangeDto>
{
    public ConfirmEmailChangeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\s*[0-9]{6}\s*$").WithMessage("Enter the six-digit code.");
    }
}

public class UpdatePhoneRequestValidator : AbstractValidator<UpdatePhoneRequestDto>
{
    public UpdatePhoneRequestValidator()
    {
        // The same rule as the profile.
        RuleFor(x => x.Phone).Matches(@"^\+?[0-9 ]{6,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Enter a valid phone number.");
    }
}
