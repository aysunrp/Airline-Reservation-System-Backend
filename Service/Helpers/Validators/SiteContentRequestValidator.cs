using Domain.Constants;
using FluentValidation;
using Service.Helpers.DTOs.SiteContent;

namespace Service.Helpers.Validators;

public class SiteContentRequestValidator : AbstractValidator<SiteContentRequestDto>
{
    public SiteContentRequestValidator()
    {
        RuleFor(x => x.TextAz)
            .NotEmpty().WithMessage("Mətn boş ola bilməz.")
            .MaximumLength(SiteContentLimits.MaxTextLength)
            .WithMessage(x => $"Mətn {SiteContentLimits.MaxTextLength} simvoldan uzun ola bilməz. İndi {x.TextAz?.Length} simvol var.");
    }
}
