using System.Text.RegularExpressions;
using FluentValidation;
using Service.Helpers.DTOs.Cms;

namespace Service.Helpers.Validators;

public static class CmsRules
{
    public static readonly string[] Sections = { "why", "premium" };
    public static readonly string[] Icons = { "booking", "seat", "trips", "status", "premium", "support" };

    private static readonly Regex SitePage = new(@"^[a-z0-9-]+\.html(#[A-Za-z0-9_-]+)?(\?[\w=&%.-]*)?$", RegexOptions.Compiled);

    /// <summary>A page of this site (booking.html, about-aerova.html#economy) or an https address; never a script or another scheme.</summary>
    public static bool IsAllowedLink(string? url)
        => string.IsNullOrWhiteSpace(url)
           || SitePage.IsMatch(url.Trim())
           || (url.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.Any(char.IsWhiteSpace));
}

public class FaqItemRequestValidator : AbstractValidator<FaqItemRequestDto>
{
    public FaqItemRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().WithMessage("Sualı yazın.").MaximumLength(300).WithMessage("Sual ən çox 300 simvol ola bilər.");
        RuleFor(x => x.Answer).NotEmpty().WithMessage("Cavabı yazın.").MaximumLength(3000).WithMessage("Cavab ən çox 3000 simvol ola bilər.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000).When(x => x.SortOrder is not null).WithMessage("Sıra 0 ilə 10000 arasında olmalıdır.");
    }
}

public class HomeCardRequestValidator : AbstractValidator<HomeCardRequestDto>
{
    public HomeCardRequestValidator()
    {
        RuleFor(x => x.Section).Must(section => CmsRules.Sections.Contains(section)).WithMessage("Bölmə \"why\" və ya \"premium\" olmalıdır.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlığı yazın.").MaximumLength(200).WithMessage("Başlıq ən çox 200 simvol ola bilər.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Təsviri yazın.").MaximumLength(800).WithMessage("Təsvir ən çox 800 simvol ola bilər.");
        RuleFor(x => x.Label).MaximumLength(100).WithMessage("Etiket ən çox 100 simvol ola bilər.");
        RuleFor(x => x.LinkText).MaximumLength(100).WithMessage("Düymə mətni ən çox 100 simvol ola bilər.");
        RuleFor(x => x.LinkUrl).MaximumLength(300).WithMessage("Keçid ən çox 300 simvol ola bilər.")
            .Must(CmsRules.IsAllowedLink).WithMessage("Keçid saytın səhifəsi (məsələn booking.html) və ya https ünvanı olmalıdır.");
        RuleFor(x => x.IconKey).Must(icon => string.IsNullOrEmpty(icon) || CmsRules.Icons.Contains(icon))
            .WithMessage("İkon bunlardan biri olmalıdır: " + string.Join(", ", CmsRules.Icons) + ".");
        RuleFor(x => x.ImageAlt).MaximumLength(200).WithMessage("Şəklin təsviri ən çox 200 simvol ola bilər.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000).When(x => x.SortOrder is not null).WithMessage("Sıra 0 ilə 10000 arasında olmalıdır.");
    }
}
