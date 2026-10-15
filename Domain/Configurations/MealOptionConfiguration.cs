using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class MealOptionConfiguration : IEntityTypeConfiguration<MealOption>
{
    public void Configure(EntityTypeBuilder<MealOption> builder)
    {
        builder.Property(x => x.Code).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
