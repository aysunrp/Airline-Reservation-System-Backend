using FluentValidation;
using Service.Helpers.DTOs.Newsletter;

namespace Service.Helpers.Validators;

public class NewsletterSubscribeRequestValidator : AbstractValidator<NewsletterSubscribeRequestDto>
{
    public NewsletterSubscribeRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(x => x.Language).MaximumLength(5);
    }
}

public class NewsletterTokenRequestValidator : AbstractValidator<NewsletterTokenRequestDto>
{
    public NewsletterTokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
    }
}
