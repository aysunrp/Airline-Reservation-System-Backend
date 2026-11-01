using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories.Interfaces;

namespace Repository.Repositories;

public class BookingRepository : BaseRepository<Booking>, IBookingRepository
{
    public BookingRepository(AppDbContext context) : base(context)
    {
    }

    public Task<Booking?> GetByPnrAsync(string pnr, CancellationToken cancellationToken = default)
        => Set.AsSplitQuery()
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Origin)
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Destination)
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Aircraft)
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets).ThenInclude(t => t.FlightSeat!).ThenInclude(s => s.AircraftSeat)
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets).ThenInclude(t => t.BaggageOption)
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets).ThenInclude(t => t.MealOption)
            .Include(b => b.Payments)
            .Include(b => b.Cancellation!).ThenInclude(c => c.Refunds)
            .Include(b => b.PromoCode)
            .FirstOrDefaultAsync(b => b.Pnr == pnr, cancellationToken);

    public Task<bool> PnrExistsAsync(string pnr, CancellationToken cancellationToken = default)
        => Set.AnyAsync(b => b.Pnr == pnr, cancellationToken);

    /// <summary>
    /// A few small queries, each over narrow columns and each filtered by the account: the bookings, their legs, their seats, their
    /// cancellations and refunds. They are put together here. (Loading whole bookings with flights, routes, airports and aircraft made one
    /// wide, sorted query that SQL Server sometimes held up for 25 seconds waiting for a memory grant.)
    /// </summary>
    public async Task<IReadOnlyList<BookingSummaryRow>> GetSummariesByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var bookings = await Set.AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
            .Select(b => new { b.Id, b.Pnr, b.Status, b.TripType, b.TotalAmount, b.CreatedAt, PassengerCount = b.Passengers.Count })
            .ToListAsync(cancellationToken);
        if (bookings.Count == 0)
        {
            return Array.Empty<BookingSummaryRow>();
        }

        var legs = await Context.Set<BookingFlight>().AsNoTracking()
            .Where(f => f.Booking.UserId == userId)
            .Select(f => new
            {
                f.BookingId, f.Id, f.LegOrder, f.JourneyNo, f.FlightId, f.CabinClass,
                f.Flight.FlightNumber, f.Flight.DepartureAt, f.Flight.ArrivalAt,
                OriginCode = f.Flight.Route.Origin.Code, OriginCity = f.Flight.Route.Origin.City,
                DestinationCode = f.Flight.Route.Destination.Code, DestinationCity = f.Flight.Route.Destination.City,
                AircraftModel = f.Flight.Aircraft.Model
            })
            .ToListAsync(cancellationToken);

        var seats = await Context.Set<Ticket>().AsNoTracking()
            .Where(t => t.BookingFlight.Booking.UserId == userId && t.FlightSeatId != null)
            .OrderBy(t => t.BookingPassengerId).ThenBy(t => t.Id)
            .Select(t => new { t.BookingFlightId, t.FlightSeat!.AircraftSeat.RowNumber, t.FlightSeat.AircraftSeat.Letter })
            .ToListAsync(cancellationToken);

        var cancellations = await Context.Set<Cancellation>().AsNoTracking()
            .Where(c => c.Booking.UserId == userId)
            .Select(c => new { c.Id, c.BookingId, c.RefundAmount })
            .ToListAsync(cancellationToken);

        var refunds = cancellations.Count == 0
            ? new List<RefundRow>()
            : await Context.Set<Refund>().AsNoTracking()
                .Where(r => r.Cancellation.Booking.UserId == userId)
                .Select(r => new RefundRow(r.CancellationId, r.Status, r.Currency, r.CreatedAt, r.Payment.Method))
                .ToListAsync(cancellationToken);

        var seatsByLeg = seats.GroupBy(s => s.BookingFlightId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(s => s.RowNumber + s.Letter).ToList());
        var legsByBooking = legs.GroupBy(l => l.BookingId).ToDictionary(g => g.Key);
        var cancellationByBooking = cancellations.ToDictionary(c => c.BookingId);

        return bookings.Select(b =>
        {
            cancellationByBooking.TryGetValue(b.Id, out var cancellation);
            var latest = cancellation is null
                ? null
                : refunds.Where(r => r.CancellationId == cancellation.Id).OrderByDescending(r => r.CreatedAt).FirstOrDefault();

            var bookingLegs = legsByBooking.TryGetValue(b.Id, out var group)
                ? group.OrderBy(l => l.LegOrder).Select(l => new BookingLegRow(
                    l.LegOrder, l.FlightId, l.FlightNumber, l.OriginCode, l.OriginCity, l.DestinationCode, l.DestinationCity,
                    l.DepartureAt, l.ArrivalAt, l.CabinClass, l.AircraftModel,
                    seatsByLeg.TryGetValue(l.Id, out var labels) ? labels : Array.Empty<string>(), l.JourneyNo)).ToList()
                : new List<BookingLegRow>();

            return new BookingSummaryRow(b.Id, b.Pnr, b.Status, b.TripType, b.TotalAmount, b.CreatedAt, b.PassengerCount,
                cancellation?.RefundAmount, latest?.Status, latest?.Currency, latest is not null && latest.Method != PaymentMethod.StripeCheckout, bookingLegs);
        }).ToList();
    }

    private sealed record RefundRow(int CancellationId, RefundStatus Status, string Currency, DateTime CreatedAt, PaymentMethod Method);

    public async Task<IReadOnlyList<Booking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Origin)
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Destination)
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Aircraft)
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets).ThenInclude(t => t.FlightSeat!).ThenInclude(s => s.AircraftSeat)
            .Include(b => b.Cancellation!).ThenInclude(c => c.Refunds).ThenInclude(r => r.Payment)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetForAdminAsync(string? pnr, string? passengerName, BookingStatus? status, int? take = null, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsSplitQuery()
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Origin)
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Destination)
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets).ThenInclude(t => t.FlightSeat!).ThenInclude(s => s.AircraftSeat)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pnr))
        {
            var fragment = pnr.Trim();
            query = query.Where(b => b.Pnr.Contains(fragment));
        }

        if (!string.IsNullOrWhiteSpace(passengerName))
        {
            var fragment = passengerName.Trim();
            query = query.Where(b => b.Passengers.Any(p => (p.FirstName + " " + p.LastName).Contains(fragment)));
        }

        if (status is not null)
        {
            query = query.Where(b => b.Status == status);
        }

        query = query.OrderByDescending(b => b.CreatedAt);
        if (take is not null)
        {
            query = query.Take(take.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetUserIdsWithActiveBookingsOnFlightAsync(int flightId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(b => b.UserId != null
                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                        && b.Flights.Any(f => f.FlightId == flightId))
            .Select(b => b.UserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetWithOpenRefundsAsync(CancellationToken cancellationToken = default)
        => await Set.AsSplitQuery()
            .Include(b => b.Payments)
            .Include(b => b.Cancellation!).ThenInclude(c => c.Refunds)
            .Where(b => b.Cancellation != null
                        && b.Cancellation.Refunds.Any(r => r.Status == RefundStatus.Pending || r.Status == RefundStatus.Processing))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetConfirmedWithLandedFlightsAsync(DateTime now, CancellationToken cancellationToken = default)
        => await Set.AsSplitQuery()
            .Include(b => b.Flights).ThenInclude(f => f.Flight)
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets)
            .Where(b => b.Status == BookingStatus.Confirmed && b.Flights.Any(f => f.Flight.ArrivalAt <= now))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetConfirmedDepartingBetweenAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().AsSplitQuery()
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Origin)
            .Include(b => b.Flights).ThenInclude(f => f.Flight).ThenInclude(f => f.Route).ThenInclude(r => r.Destination)
            .Where(b => b.Status == BookingStatus.Confirmed && b.UserId != null
                        && b.Flights.Any(f => f.Flight.DepartureAt > from && f.Flight.DepartureAt <= to))
            .ToListAsync(cancellationToken);

    public Task<int> CountPaidAsync(CancellationToken cancellationToken = default)
        => Set.CountAsync(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed, cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetExpiredPendingAsync(DateTime now, CancellationToken cancellationToken = default)
        => await Set.AsSplitQuery()
            .Include(b => b.Passengers).ThenInclude(p => p.Tickets)
            .Include(b => b.Payments)
            .Where(b => b.Status == BookingStatus.Pending && b.ExpiresAt != null && b.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
}
