using FluentValidation;
using Service.Helpers.DTOs.Account;

namespace Service.Helpers.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
