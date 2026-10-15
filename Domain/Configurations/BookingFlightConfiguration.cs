using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class BookingFlightConfiguration : IEntityTypeConfiguration<BookingFlight>
{
    public void Configure(EntityTypeBuilder<BookingFlight> builder)
    {
        builder.Property(x => x.CabinClass).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Booking).WithMany(x => x.Flights).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Flight).WithMany().HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BookingId, x.LegOrder }).IsUnique();
    }
}
