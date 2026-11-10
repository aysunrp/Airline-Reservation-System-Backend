using Domain.Constants;
using FluentValidation;
using Service.Helpers.DTOs.SiteContent;
using Service.Helpers.Seeding;

namespace Service.Helpers.Validators;

public class SiteContentCreateRequestValidator : AbstractValidator<SiteContentCreateRequestDto>
{
    public SiteContentCreateRequestValidator()
    {
        RuleFor(x => x.Page)
            .NotEmpty().WithMessage("Səhifə tələb olunur.")
            .Must(page => SiteContentData.IsManagedCmsPage(page!)).WithMessage("Bu səhifə üçün mətn əlavə edilə bilməz.");

        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Açar tələb olunur.")
            .MaximumLength(120).WithMessage("Açar 120 simvoldan uzun ola bilməz.")
            .Must(key => key != null && System.Text.RegularExpressions.Regex.IsMatch(key, @"^[\w.\-]+$"))
            .WithMessage("Açar yalnız hərf, rəqəm, nöqtə və tire ehtiva edə bilər.");

        RuleFor(x => x.TextAz)
            .NotEmpty().WithMessage("Mətn boş ola bilməz.")
            .MaximumLength(SiteContentLimits.MaxTextLength)
            .WithMessage(x => $"Mətn {SiteContentLimits.MaxTextLength} simvoldan uzun ola bilməz. İndi {x.TextAz?.Length} simvol var.");
    }
}
