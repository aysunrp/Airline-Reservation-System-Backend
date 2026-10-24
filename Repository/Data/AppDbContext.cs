using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Repository.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Aircraft> Aircrafts => Set<Aircraft>();
    public DbSet<AircraftSeat> AircraftSeats => Set<AircraftSeat>();
    public DbSet<BaggageOption> BaggageOptions => Set<BaggageOption>();
    public DbSet<MealOption> MealOptions => Set<MealOption>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<CancellationRule> CancellationRules => Set<CancellationRule>();
    public DbSet<FlightSchedule> FlightSchedules => Set<FlightSchedule>();
    public DbSet<FlightScheduleFare> FlightScheduleFares => Set<FlightScheduleFare>();
    public DbSet<FlightScheduleStop> FlightScheduleStops => Set<FlightScheduleStop>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<FlightSegment> FlightSegments => Set<FlightSegment>();
    public DbSet<FlightFare> FlightFares => Set<FlightFare>();
    public DbSet<FlightSeat> FlightSeats => Set<FlightSeat>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SavedPassenger> SavedPassengers => Set<SavedPassenger>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingFlight> BookingFlights => Set<BookingFlight>();
    public DbSet<BookingPassenger> BookingPassengers => Set<BookingPassenger>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Cancellation> Cancellations => Set<Cancellation>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailToken> EmailTokens => Set<EmailToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Domain.Configurations contains one IEntityTypeConfiguration<T> per entity.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Domain.Entities.Airport).Assembly);

        // A seat can be held by only one non-cancelled ticket per flight leg.
        // Relational filtered indexes are provider specific, so this lives in the Repository layer.
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.FlightSeatId)
            .IsUnique()
            .HasFilter($"[FlightSeatId] IS NOT NULL AND [Status] <> '{nameof(TicketStatus.Cancelled)}'");

        // A schedule produces at most one flight per operating day. Flights created by hand have no
        // scheduled date and stay unrestricted (SQL Server would otherwise treat NULLs as equal).
        modelBuilder.Entity<Flight>()
            .HasIndex(f => new { f.FlightScheduleId, f.ScheduledDate })
            .IsUnique()
            .HasFilter("[FlightScheduleId] IS NOT NULL AND [ScheduledDate] IS NOT NULL");
    }
}
