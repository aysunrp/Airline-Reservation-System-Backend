using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.Property(x => x.TicketNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.TicketNumber).IsUnique();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(150);
        builder.Property(x => x.BookingPnr).HasMaxLength(12);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.UserId, x.LastMessageAt });
        builder.HasIndex(x => new { x.Status, x.LastMessageAt });
        builder.HasIndex(x => x.Category);
    }
}

public class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessage>
{
    public void Configure(EntityTypeBuilder<SupportMessage> builder)
    {
        builder.Property(x => x.AuthorType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(4000);
        builder.HasOne(x => x.Ticket).WithMany(t => t.Messages).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TicketId, x.CreatedAt });
    }
}
