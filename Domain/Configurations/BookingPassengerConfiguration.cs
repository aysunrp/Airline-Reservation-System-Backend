using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class BookingPassengerConfiguration : IEntityTypeConfiguration<BookingPassenger>
{
    public void Configure(EntityTypeBuilder<BookingPassenger> builder)
    {
        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.NationalityCode).IsRequired().HasMaxLength(2);
        builder.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DocumentNumber).IsRequired().HasMaxLength(30);
        builder.Property(x => x.DocumentIssuingCountry).IsRequired().HasMaxLength(2);
        builder.Property(x => x.PassengerType).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Booking).WithMany(x => x.Passengers).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SavedPassenger).WithMany().HasForeignKey(x => x.SavedPassengerId).OnDelete(DeleteBehavior.SetNull);
    }
}
