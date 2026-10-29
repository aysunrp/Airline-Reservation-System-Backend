using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class NewsletterSubscriberConfiguration : IEntityTypeConfiguration<NewsletterSubscriber>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscriber> builder)
    {
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Language).IsRequired().HasMaxLength(2);
        builder.Property(x => x.ConfirmationTokenHash).HasMaxLength(64);
        builder.HasIndex(x => x.ConfirmationTokenHash).IsUnique();
        builder.HasIndex(x => x.UnsubscribeKey).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
