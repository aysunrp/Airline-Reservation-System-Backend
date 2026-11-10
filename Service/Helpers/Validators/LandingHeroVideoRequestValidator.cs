using FluentValidation;
using Service.Helpers.DTOs.LandingHeroVideo;

namespace Service.Helpers.Validators;

public class LandingHeroVideoRequestValidator : AbstractValidator<LandingHeroVideoRequestDto>
{
    public LandingHeroVideoRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(Domain.Constants.LandingHeroVideoLimits.MaxTitleLength);
        RuleFor(x => x.InternalNotes).MaximumLength(Domain.Constants.LandingHeroVideoLimits.MaxNotesLength);
        RuleFor(x => x.VideoUrl).MaximumLength(Domain.Constants.LandingHeroVideoLimits.MaxUrlLength);
        RuleFor(x => x.PosterUrl).MaximumLength(Domain.Constants.LandingHeroVideoLimits.MaxUrlLength);
        RuleFor(x => x.MobileVideoUrl).MaximumLength(Domain.Constants.LandingHeroVideoLimits.MaxUrlLength);
        RuleFor(x => x.SourceName).MaximumLength(Domain.Constants.LandingHeroVideoLimits.MaxSourceNameLength);
        RuleFor(x => x.SourceUrl).MaximumLength(Domain.Constants.LandingHeroVideoLimits.MaxUrlLength);
        RuleFor(x => x.LicenseInfo).MaximumLength(Domain.Constants.LandingHeroVideoLimits.MaxLicenseLength);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 9999).When(x => x.SortOrder.HasValue);
    }
}
