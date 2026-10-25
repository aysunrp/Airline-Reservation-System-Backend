using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(x => x.ActorRole).HasMaxLength(20);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(60);
        builder.Property(x => x.TargetType).IsRequired().HasMaxLength(40);
        builder.Property(x => x.TargetId).HasMaxLength(64);
        builder.Property(x => x.Details).HasMaxLength(500);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.ActorUserId);
    }
}
