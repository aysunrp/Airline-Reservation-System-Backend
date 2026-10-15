using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class FlightScheduleConfiguration : IEntityTypeConfiguration<FlightSchedule>
{
    public void Configure(EntityTypeBuilder<FlightSchedule> builder)
    {
        builder.Property(x => x.FlightNumber).IsRequired().HasMaxLength(10);
        builder.HasOne(x => x.Route).WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DefaultAircraft).WithMany().HasForeignKey(x => x.DefaultAircraftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.FlightNumber);
    }
}
