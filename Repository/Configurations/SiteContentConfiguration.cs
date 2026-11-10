using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repository.Configurations;

public class SiteContentConfiguration : IEntityTypeConfiguration<SiteContent>
{
    public void Configure(EntityTypeBuilder<SiteContent> builder)
    {
        builder.Property(x => x.Page).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(100);
        builder.Property(x => x.TextAz).IsRequired().HasMaxLength(SiteContentLimits.MaxTextLength);
        builder.HasIndex(x => new { x.Page, x.Key }).IsUnique();
    }
}
