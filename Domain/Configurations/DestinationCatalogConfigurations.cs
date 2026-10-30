using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.Property(x => x.Code).IsRequired().HasMaxLength(2);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.NameAz).IsRequired().HasMaxLength(100);
        builder.Property(x => x.NameRu).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(400);
        builder.Property(x => x.DescriptionAz).IsRequired().HasMaxLength(400);
        builder.Property(x => x.DescriptionRu).IsRequired().HasMaxLength(400);
        builder.Property(x => x.ImageFile).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class DestinationCityConfiguration : IEntityTypeConfiguration<DestinationCity>
{
    public void Configure(EntityTypeBuilder<DestinationCity> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.NameAz).IsRequired().HasMaxLength(100);
        builder.Property(x => x.NameRu).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Aliases).HasMaxLength(300);
        builder.Property(x => x.ImageFile).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(400);
        builder.Property(x => x.DescriptionAz).IsRequired().HasMaxLength(400);
        builder.Property(x => x.DescriptionRu).IsRequired().HasMaxLength(400);
        builder.HasIndex(x => new { x.CountryId, x.Name }).IsUnique();
        // Countries are switched off, not deleted, so a country with cities cannot be removed.
        builder.HasOne(x => x.Country).WithMany(c => c.Cities).HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
    }
}
