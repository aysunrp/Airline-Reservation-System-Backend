using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class FlightFareConfiguration : IEntityTypeConfiguration<FlightFare>
{
    public void Configure(EntityTypeBuilder<FlightFare> builder)
    {
        builder.Property(x => x.CabinClass).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BasePrice).HasPrecision(18, 2);
        builder.HasOne(x => x.Flight).WithMany(x => x.Fares).HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.FlightId, x.CabinClass }).IsUnique();
    }
}
