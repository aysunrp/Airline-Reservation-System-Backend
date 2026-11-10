using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repository.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.Property(x => x.SessionKey).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Device).HasMaxLength(120);
        builder.Property(x => x.MaskedIp).HasMaxLength(64);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.SessionKey).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.RevokedAt });
    }
}
