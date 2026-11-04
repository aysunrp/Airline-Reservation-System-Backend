using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class HomeCardConfiguration : IEntityTypeConfiguration<HomeCard>
{
    public void Configure(EntityTypeBuilder<HomeCard> builder)
    {
        builder.Property(x => x.Section).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Label).HasMaxLength(100);
        builder.Property(x => x.LinkText).HasMaxLength(100);
        builder.Property(x => x.LinkUrl).HasMaxLength(300);
        builder.Property(x => x.IconKey).HasMaxLength(30);
        builder.Property(x => x.ImagePath).HasMaxLength(300);
        builder.Property(x => x.ImageAlt).HasMaxLength(200);
        builder.HasIndex(x => new { x.Section, x.SortOrder });
    }
}
