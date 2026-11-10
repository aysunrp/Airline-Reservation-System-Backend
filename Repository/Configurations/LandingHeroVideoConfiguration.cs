using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repository.Configurations;

public class LandingHeroVideoConfiguration : IEntityTypeConfiguration<LandingHeroVideo>
{
    public void Configure(EntityTypeBuilder<LandingHeroVideo> builder)
    {
        builder.Property(x => x.Title).IsRequired().HasMaxLength(LandingHeroVideoLimits.MaxTitleLength);
        builder.Property(x => x.InternalNotes).HasMaxLength(LandingHeroVideoLimits.MaxNotesLength);
        builder.Property(x => x.VideoUrl).IsRequired().HasMaxLength(LandingHeroVideoLimits.MaxUrlLength);
        builder.Property(x => x.PosterUrl).HasMaxLength(LandingHeroVideoLimits.MaxUrlLength);
        builder.Property(x => x.MobileVideoUrl).HasMaxLength(LandingHeroVideoLimits.MaxUrlLength);
        builder.Property(x => x.SourceName).HasMaxLength(LandingHeroVideoLimits.MaxSourceNameLength);
        builder.Property(x => x.SourceUrl).HasMaxLength(LandingHeroVideoLimits.MaxUrlLength);
        builder.Property(x => x.LicenseInfo).HasMaxLength(LandingHeroVideoLimits.MaxLicenseLength);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.SortOrder);
    }
}
