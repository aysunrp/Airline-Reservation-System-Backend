using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class SavedPassengerConfiguration : IEntityTypeConfiguration<SavedPassenger>
{
    public void Configure(EntityTypeBuilder<SavedPassenger> builder)
    {
        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Relationship).HasMaxLength(30);
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.NationalityCode).HasMaxLength(2);
        builder.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DocumentNumber).HasMaxLength(30);
        builder.Property(x => x.DocumentIssuingCountry).HasMaxLength(2);
        builder.HasOne(x => x.User).WithMany(x => x.SavedPassengers).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
