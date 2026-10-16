using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Repository.Repositories.Interfaces;

namespace Service.Tests;

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;
}

internal class FakeRepository<T> : IBaseRepository<T> where T : BaseEntity
{
    protected readonly List<T> Items = new();

    public FakeRepository(params T[] items) => Items.AddRange(items);

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<T>>(Items.ToList());

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == 0)
        {
            entity.Id = Items.Count == 0 ? 1 : Items.Max(x => x.Id) + 1;
        }

        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity)
    {
    }

    public void Delete(T entity) => Items.Remove(entity);

    public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class FakePromoCodeRepository : FakeRepository<PromoCode>, IPromoCodeRepository
{
    public FakePromoCodeRepository(params PromoCode[] items) : base(items)
    {
    }

    public Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(p => p.Code == code));
}

internal sealed class FakeUserRepository : FakeRepository<User>, IUserRepository
{
    public FakeUserRepository(params User[] items) : base(items)
    {
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(u => u.Email == email));

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(u => u.Email == email));
}

internal sealed class FakeSavedPassengerRepository : FakeRepository<SavedPassenger>, ISavedPassengerRepository
{
    public FakeSavedPassengerRepository(params SavedPassenger[] items) : base(items)
    {
    }

    public Task<IReadOnlyList<SavedPassenger>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SavedPassenger>>(Items.Where(p => p.UserId == userId).ToList());
}

internal sealed class FakeNotificationRepository : FakeRepository<Notification>, INotificationRepository
{
    public FakeNotificationRepository(params Notification[] items) : base(items)
    {
    }

    public Task<IReadOnlyList<Notification>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Notification>>(Items.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ToList());

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Count(n => n.UserId == userId && !n.IsRead));

    public Task<IReadOnlyList<Notification>> GetUnreadTrackedAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Notification>>(Items.Where(n => n.UserId == userId && !n.IsRead).ToList());
}

internal sealed class FakeCancellationRuleRepository : FakeRepository<CancellationRule>, ICancellationRuleRepository
{
    public FakeCancellationRuleRepository(params CancellationRule[] items) : base(items)
    {
    }

    public Task<IReadOnlyList<CancellationRule>> GetByCabinClassAsync(CabinClass cabinClass, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CancellationRule>>(Items.Where(r => r.CabinClass == cabinClass).ToList());
}

internal sealed class FakeAirportRepository : FakeRepository<Airport>, IAirportRepository
{
    public FakeAirportRepository(params Airport[] items) : base(items)
    {
    }

    public Task<Airport?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(a => a.Code == code));

    public Task<IReadOnlyList<Airport>> GetActiveAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Airport>>(Items.Where(a => a.IsActive).ToList());
}

internal sealed class FakeFlightRepository : FakeRepository<Flight>, IFlightRepository
{
    public FakeFlightRepository(params Flight[] items) : base(items)
    {
    }

    public Task<IReadOnlyList<Flight>> SearchAsync(int originAirportId, int destinationAirportId, DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Flight>>(Items
            .Where(f => f.Route.OriginAirportId == originAirportId
                        && f.Route.DestinationAirportId == destinationAirportId
                        && DateOnly.FromDateTime(f.DepartureAt) == date
                        && f.Status != FlightStatus.Cancelled)
            .OrderBy(f => f.DepartureAt)
            .ToList());

    public Task<Flight?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(f => f.Id == id));
}

internal sealed class FakeFlightSeatRepository : FakeRepository<FlightSeat>, IFlightSeatRepository
{
    public FakeFlightSeatRepository(params FlightSeat[] items) : base(items)
    {
    }

    public Task<IReadOnlyList<FlightSeat>> GetByFlightIdAsync(int flightId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FlightSeat>>(Items.Where(s => s.FlightId == flightId)
            .OrderBy(s => s.AircraftSeat.RowNumber).ThenBy(s => s.AircraftSeat.Letter).ToList());

    public Task<IReadOnlyList<FlightSeat>> GetTrackedByIdsAsync(int flightId, IReadOnlyCollection<int> seatIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FlightSeat>>(Items.Where(s => s.FlightId == flightId && seatIds.Contains(s.Id)).ToList());

    public Task<IReadOnlyList<FlightSeat>> GetTrackedByIdsAsync(IReadOnlyCollection<int> seatIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FlightSeat>>(Items.Where(s => seatIds.Contains(s.Id)).ToList());
}

/// <summary>
/// In-memory booking store. SaveChangesAsync assigns ids and wires navigations the way EF relationship fix-up would,
/// so mapped DTOs can be built from what the service saved.
/// </summary>
internal sealed class FakeBookingRepository : FakeRepository<Booking>, IBookingRepository
{
    private readonly TestData _data;
    private int _nextId = 1000;

    public FakeBookingRepository(TestData data, params Booking[] items) : base(items) => _data = data;

    public int SaveCount { get; private set; }

    /// <summary>Resolves Booking.PromoCode like an EF include would.</summary>
    public Func<int, PromoCode?>? PromoCodeLookup { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        foreach (var booking in Items)
        {
            if (booking.PromoCodeId is int promoCodeId && PromoCodeLookup is not null)
            {
                booking.PromoCode ??= PromoCodeLookup(promoCodeId);
            }

            foreach (var leg in booking.Flights)
            {
                leg.Booking = booking;
                leg.Flight ??= _data.Flight(leg.FlightId);
            }

            foreach (var passenger in booking.Passengers)
            {
                passenger.Id = passenger.Id == 0 ? _nextId++ : passenger.Id;
                passenger.Booking = booking;
                foreach (var ticket in passenger.Tickets)
                {
                    ticket.Id = ticket.Id == 0 ? _nextId++ : ticket.Id;
                    ticket.BookingPassenger = passenger;
                    ticket.FlightSeat = ticket.FlightSeatId is int seatId ? _data.Seat(seatId) : null;
                }
            }

            foreach (var payment in booking.Payments)
            {
                payment.Id = payment.Id == 0 ? _nextId++ : payment.Id;
                payment.Booking = booking;
            }

            if (booking.Cancellation is { } cancellation)
            {
                cancellation.Id = cancellation.Id == 0 ? _nextId++ : cancellation.Id;
                cancellation.Booking = booking;
                booking.Cancellation = cancellation;
            }
        }

        return Task.FromResult(1);
    }

    public Task<Booking?> GetByPnrAsync(string pnr, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(b => b.Pnr == pnr));

    public Task<bool> PnrExistsAsync(string pnr, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(b => b.Pnr == pnr));

    public Task<IReadOnlyList<Booking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Booking>>(Items.Where(b => b.UserId == userId).OrderByDescending(b => b.CreatedAt).ToList());

    public Task<IReadOnlyList<Booking>> GetExpiredPendingAsync(DateTime now, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Booking>>(Items.Where(b => b.Status == BookingStatus.Pending && b.ExpiresAt <= now).ToList());
}

internal sealed class FakeAircraftRepository : FakeRepository<Aircraft>, IAircraftRepository
{
    public FakeAircraftRepository(params Aircraft[] items) : base(items)
    {
    }

    public HashSet<int> InUse { get; } = new();

    public Task<IReadOnlyList<Aircraft>> GetAllWithSeatsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Aircraft>>(Items.OrderBy(a => a.Name).ToList());

    public Task<Aircraft?> GetWithSeatsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    public Task<bool> RegistrationExistsAsync(string registrationNumber, int? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(a => a.RegistrationNumber == registrationNumber && a.Id != excludeId));

    public Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(InUse.Contains(id));
}
