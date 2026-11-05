using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class LandingHeroVideoConfiguration : IEntityTypeConfiguration<LandingHeroVideo>
{
    public const int MaxTitleLength = 200;
    public const int MaxUrlLength = 2048;
    public const int MaxNotesLength = 2000;
    public const int MaxSourceNameLength = 200;
    public const int MaxLicenseLength = 500;

    public void Configure(EntityTypeBuilder<LandingHeroVideo> builder)
    {
        builder.Property(x => x.Title).IsRequired().HasMaxLength(MaxTitleLength);
        builder.Property(x => x.InternalNotes).HasMaxLength(MaxNotesLength);
        builder.Property(x => x.VideoUrl).IsRequired().HasMaxLength(MaxUrlLength);
        builder.Property(x => x.PosterUrl).HasMaxLength(MaxUrlLength);
        builder.Property(x => x.MobileVideoUrl).HasMaxLength(MaxUrlLength);
        builder.Property(x => x.SourceName).HasMaxLength(MaxSourceNameLength);
        builder.Property(x => x.SourceUrl).HasMaxLength(MaxUrlLength);
        builder.Property(x => x.LicenseInfo).HasMaxLength(MaxLicenseLength);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.SortOrder);
    }
}
