using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class AircraftSeatConfiguration : IEntityTypeConfiguration<AircraftSeat>
{
    public void Configure(EntityTypeBuilder<AircraftSeat> builder)
    {
        builder.Property(x => x.Letter).IsRequired().HasMaxLength(1);
        builder.Property(x => x.CabinClass).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Aircraft).WithMany(x => x.Seats).HasForeignKey(x => x.AircraftId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.AircraftId, x.RowNumber, x.Letter }).IsUnique();
    }
}
