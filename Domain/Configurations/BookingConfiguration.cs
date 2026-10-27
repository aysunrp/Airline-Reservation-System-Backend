using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(x => x.Pnr).IsRequired().HasMaxLength(6);
        builder.Property(x => x.ContactEmail).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ContactPhone).IsRequired().HasMaxLength(30);
        builder.Property(x => x.TripType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BaseFare).HasPrecision(18, 2);
        builder.Property(x => x.BaggageTotal).HasPrecision(18, 2);
        builder.Property(x => x.MealTotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxesTotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.User).WithMany(x => x.Bookings).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PromoCode).WithMany().HasForeignKey(x => x.PromoCodeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Pnr).IsUnique();
    }
}
