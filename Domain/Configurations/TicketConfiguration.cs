using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.Property(x => x.TicketNumber).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FareAmount).HasPrecision(18, 2);
        builder.Property(x => x.BaggagePrice).HasPrecision(18, 2);
        builder.Property(x => x.MealPrice).HasPrecision(18, 2);
        builder.HasOne(x => x.BookingPassenger).WithMany(x => x.Tickets).HasForeignKey(x => x.BookingPassengerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.BookingFlight).WithMany(x => x.Tickets).HasForeignKey(x => x.BookingFlightId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FlightSeat).WithMany().HasForeignKey(x => x.FlightSeatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BaggageOption).WithMany().HasForeignKey(x => x.BaggageOptionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.MealOption).WithMany().HasForeignKey(x => x.MealOptionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.TicketNumber).IsUnique();
        builder.HasIndex(x => new { x.BookingPassengerId, x.BookingFlightId }).IsUnique();
    }
}
