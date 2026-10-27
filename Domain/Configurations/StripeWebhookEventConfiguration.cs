using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class StripeWebhookEventConfiguration : IEntityTypeConfiguration<StripeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<StripeWebhookEvent> builder)
    {
        builder.Property(x => x.EventId).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CheckoutSessionId).HasMaxLength(255);
        builder.Property(x => x.Outcome).HasMaxLength(200);
        builder.HasIndex(x => x.EventId).IsUnique();
    }
}
