using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Title).HasMaxLength(10);
        builder.Property(x => x.NationalityCode).HasMaxLength(2);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.Email).IsUnique();
    }
}
