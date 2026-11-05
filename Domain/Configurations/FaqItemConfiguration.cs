using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class FaqItemConfiguration : IEntityTypeConfiguration<FaqItem>
{
    public void Configure(EntityTypeBuilder<FaqItem> builder)
    {
        builder.Property(x => x.Question).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Answer).IsRequired().HasMaxLength(3000);
        builder.HasIndex(x => new { x.IsActive, x.SortOrder });
    }
}
