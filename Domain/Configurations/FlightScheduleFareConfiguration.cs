using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class FlightScheduleFareConfiguration : IEntityTypeConfiguration<FlightScheduleFare>
{
    public void Configure(EntityTypeBuilder<FlightScheduleFare> builder)
    {
        builder.Property(x => x.CabinClass).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BasePrice).HasPrecision(18, 2);
        builder.HasOne(x => x.FlightSchedule).WithMany(x => x.Fares).HasForeignKey(x => x.FlightScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.FlightScheduleId, x.CabinClass }).IsUnique();
    }
}
