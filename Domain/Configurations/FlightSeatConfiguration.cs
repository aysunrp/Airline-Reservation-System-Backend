using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class FlightSeatConfiguration : IEntityTypeConfiguration<FlightSeat>
{
    public void Configure(EntityTypeBuilder<FlightSeat> builder)
    {
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Flight).WithMany(x => x.Seats).HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AircraftSeat).WithMany().HasForeignKey(x => x.AircraftSeatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FlightId, x.AircraftSeatId }).IsUnique();
        // The dashboard counts the free seats of every upcoming flight; without this the count reads the whole seat table.
        builder.HasIndex(x => new { x.Status, x.FlightId, x.HeldUntil });
    }
}
