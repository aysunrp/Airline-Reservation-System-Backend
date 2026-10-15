using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Configurations;

public class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.HasOne(x => x.Origin).WithMany().HasForeignKey(x => x.OriginAirportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Destination).WithMany().HasForeignKey(x => x.DestinationAirportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OriginAirportId, x.DestinationAirportId }).IsUnique();
    }
}
