using FluentValidation;
using Service.Helpers.DTOs.LandingHeroVideo;

namespace Service.Helpers.Validators;

public class LandingHeroVideoRequestValidator : AbstractValidator<LandingHeroVideoRequestDto>
{
    public LandingHeroVideoRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(Domain.Configurations.LandingHeroVideoConfiguration.MaxTitleLength);
        RuleFor(x => x.InternalNotes).MaximumLength(Domain.Configurations.LandingHeroVideoConfiguration.MaxNotesLength);
        RuleFor(x => x.VideoUrl).MaximumLength(Domain.Configurations.LandingHeroVideoConfiguration.MaxUrlLength);
        RuleFor(x => x.PosterUrl).MaximumLength(Domain.Configurations.LandingHeroVideoConfiguration.MaxUrlLength);
        RuleFor(x => x.MobileVideoUrl).MaximumLength(Domain.Configurations.LandingHeroVideoConfiguration.MaxUrlLength);
        RuleFor(x => x.SourceName).MaximumLength(Domain.Configurations.LandingHeroVideoConfiguration.MaxSourceNameLength);
        RuleFor(x => x.SourceUrl).MaximumLength(Domain.Configurations.LandingHeroVideoConfiguration.MaxUrlLength);
        RuleFor(x => x.LicenseInfo).MaximumLength(Domain.Configurations.LandingHeroVideoConfiguration.MaxLicenseLength);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 9999).When(x => x.SortOrder.HasValue);
    }
}
