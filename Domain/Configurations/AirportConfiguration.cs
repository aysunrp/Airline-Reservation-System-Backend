using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class AirportConfiguration : IEntityTypeConfiguration<Airport>
{
    public void Configure(EntityTypeBuilder<Airport> builder)
    {
        builder.Property(x => x.Code).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.City).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CountryCode).IsRequired().HasMaxLength(2);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
