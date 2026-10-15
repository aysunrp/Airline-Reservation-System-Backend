using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> builder)
    {
        builder.Property(x => x.FlightNumber).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.FlightSchedule).WithMany().HasForeignKey(x => x.FlightScheduleId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Route).WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Aircraft).WithMany().HasForeignKey(x => x.AircraftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FlightNumber, x.DepartureAt }).IsUnique();
        builder.HasIndex(x => new { x.RouteId, x.DepartureAt });
    }
}
