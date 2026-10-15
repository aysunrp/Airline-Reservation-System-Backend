using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class FlightSegmentConfiguration : IEntityTypeConfiguration<FlightSegment>
{
    public void Configure(EntityTypeBuilder<FlightSegment> builder)
    {
        builder.HasOne(x => x.Flight).WithMany(x => x.Segments).HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Origin).WithMany().HasForeignKey(x => x.OriginAirportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Destination).WithMany().HasForeignKey(x => x.DestinationAirportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FlightId, x.SequenceNo }).IsUnique();
    }
}
