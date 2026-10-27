using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class CancellationConfiguration : IEntityTypeConfiguration<Cancellation>
{
    public void Configure(EntityTypeBuilder<Cancellation> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Language).HasMaxLength(5);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.FeeAmount).HasPrecision(18, 2);
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2);
        builder.HasOne(x => x.Booking).WithOne(x => x.Cancellation).HasForeignKey<Cancellation>(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
    }
}
