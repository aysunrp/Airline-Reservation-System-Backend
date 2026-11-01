using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class SiteContentConfiguration : IEntityTypeConfiguration<SiteContent>
{
    /// <summary>The longest text a page text may have (the size of the column).</summary>
    public const int MaxTextLength = 1000;

    public void Configure(EntityTypeBuilder<SiteContent> builder)
    {
        builder.Property(x => x.Page).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(100);
        builder.Property(x => x.TextAz).IsRequired().HasMaxLength(MaxTextLength);
        builder.HasIndex(x => new { x.Page, x.Key }).IsUnique();
    }
}
