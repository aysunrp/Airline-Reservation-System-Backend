using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class FlightScheduleStopConfiguration : IEntityTypeConfiguration<FlightScheduleStop>
{
    public void Configure(EntityTypeBuilder<FlightScheduleStop> builder)
    {
        builder.HasOne(x => x.FlightSchedule).WithMany(x => x.Stops).HasForeignKey(x => x.FlightScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Airport).WithMany().HasForeignKey(x => x.AirportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FlightScheduleId, x.SequenceNo }).IsUnique();
    }
}
