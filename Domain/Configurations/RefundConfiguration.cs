using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ProviderReference).HasMaxLength(100);
        builder.Property(x => x.ProviderStatus).HasMaxLength(40);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.FailureReason).HasMaxLength(300);

        builder.HasOne(x => x.Cancellation).WithMany(x => x.Refunds).HasForeignKey(x => x.CancellationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InitiatedByUser).WithMany().HasForeignKey(x => x.InitiatedByUserId).OnDelete(DeleteBehavior.SetNull);

        // One refund per cancelled booking and payment: a retry reuses the row, so a second one can never be added.
        builder.HasIndex(x => new { x.CancellationId, x.PaymentId }).IsUnique();

        // A Stripe refund belongs to exactly one row, which is how the webhook finds it. SQL Server leaves out rows without a value from a unique index on a nullable column.
        builder.HasIndex(x => x.ProviderReference).IsUnique();
    }
}
