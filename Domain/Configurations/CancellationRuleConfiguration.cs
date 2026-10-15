using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class CancellationRuleConfiguration : IEntityTypeConfiguration<CancellationRule>
{
    public void Configure(EntityTypeBuilder<CancellationRule> builder)
    {
        builder.Property(x => x.CabinClass).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FeePercent).HasPrecision(5, 2);
        builder.HasIndex(x => new { x.CabinClass, x.MinDaysBeforeDeparture }).IsUnique();
    }
}
