using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Account;
using Service.Services.Interfaces;

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

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<T>>(Items.ToList());

    public virtual Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
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

    public HashSet<int> InUse { get; } = new();

    public Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(p => p.Code == code && p.Id != excludeId));

    public Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(InUse.Contains(id));
}

internal sealed class FakeUserRepository : FakeRepository<User>, IUserRepository
{
    public FakeUserRepository(params User[] items) : base(items)
    {
    }

    public User? Find(int id) => Items.FirstOrDefault(u => u.Id == id);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(u => u.Email == email));

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(u => u.Email == email));

    public Task<IReadOnlyList<User>> SearchAsync(string? search, UserRole? role, bool? isActive, int take, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim();
        return Task.FromResult<IReadOnlyList<User>>(Items
            .Where(u => string.IsNullOrEmpty(term) || u.Email.Contains(term, StringComparison.OrdinalIgnoreCase) || u.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) || u.LastName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Where(u => role is null || u.Role == role)
            .Where(u => isActive is null || u.IsActive == isActive)
            .OrderByDescending(u => u.Id)
            .Take(take)
            .ToList());
    }

    public Task<int> CountActiveInRoleAsync(UserRole role, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Count(u => u.Role == role && u.IsActive));

    public Task<IReadOnlyList<int>> GetActiveStaffIdsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<int>>(Items.Where(u => u.IsActive && u.Role is UserRole.Admin or UserRole.SuperAdmin).Select(u => u.Id).ToList());

    /// <summary>Ids of the accounts that have bookings.</summary>
    public HashSet<int> WithBookings { get; } = new();

    public Task<bool> HasBookingsAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(WithBookings.Contains(userId));

    public Task<UserState?> GetStateAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Where(u => u.Id == id).Select(u => new UserState(u.Role, u.IsActive)).FirstOrDefault());
}

internal sealed class FakeAuditLogRepository : FakeRepository<AuditLog>, IAuditLogRepository
{
    public IReadOnlyList<AuditLog> Entries => Items;

    public Task<IReadOnlyList<AuditLog>> SearchAsync(string? action, int? actorUserId, string? targetType, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AuditLog>>(Items
            .Where(a => string.IsNullOrWhiteSpace(action) || a.Action == action.Trim())
            .Where(a => actorUserId is null || a.ActorUserId == actorUserId)
            .Where(a => string.IsNullOrWhiteSpace(targetType) || a.TargetType == targetType.Trim())
            .OrderByDescending(a => a.Id)
            .Take(take)
            .ToList());
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

    public Task<IReadOnlyList<Notification>> GetByUserIdTrackedAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Notification>>(Items.Where(n => n.UserId == userId).ToList());

    public Task<IReadOnlySet<int>> GetBookingIdsWithTypeAsync(NotificationType type, IReadOnlyCollection<int> bookingIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<int>>(Items
            .Where(n => n.Type == type && n.BookingId != null && bookingIds.Contains(n.BookingId.Value))
            .Select(n => n.BookingId!.Value)
            .ToHashSet());

    public Task<IReadOnlyList<Notification>> GetDueForEmailAsync(DateTime createdAfter, int maxAttempts, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Notification>>(Items
            .Where(n => n.EmailSentAt == null && n.EmailAttempts < maxAttempts && n.CreatedAt > createdAfter && n.User?.EmailVerifiedAt != null)
            .OrderBy(n => n.Id)
            .Take(take)
            .ToList());

    public Task<bool> EmailKeyExistsAsync(string emailKey, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(n => n.EmailKey == emailKey));
}

internal sealed class FakeCancellationRuleRepository : FakeRepository<CancellationRule>, ICancellationRuleRepository
{
    public Task<bool> ExistsAsync(CabinClass cabinClass, int minDaysBeforeDeparture, int? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(r => r.CabinClass == cabinClass && r.MinDaysBeforeDeparture == minDaysBeforeDeparture && r.Id != excludeId));

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

    public HashSet<int> InUse { get; } = new();

    public Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(a => a.Code == code && a.Id != excludeId));

    public Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(InUse.Contains(id));
}

internal sealed class FakeFlightRepository : FakeRepository<Flight>, IFlightRepository
{
    public FakeFlightRepository(params Flight[] items) : base(items)
    {
    }

    public HashSet<int> WithBookings { get; } = new();

    /// <summary>Resolve navigations on save like EF fix-up would (used by the admin flight tests).</summary>
    public Func<int, Route?>? RouteLookup { get; set; }
    public Func<int, Aircraft?>? AircraftLookup { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var flight in Items)
        {
            if (RouteLookup?.Invoke(flight.RouteId) is { } route)
            {
                flight.Route = route;
                foreach (var segment in flight.Segments)
                {
                    segment.Origin ??= route.Origin;
                    segment.Destination ??= route.Destination;
                }
            }

            if (AircraftLookup?.Invoke(flight.AircraftId) is { } aircraft)
            {
                flight.Aircraft = aircraft;
            }
        }

        return Task.FromResult(1);
    }

    public Task<IReadOnlyList<Flight>> SearchAsync(int originAirportId, int destinationAirportId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Flight>>(Items
            .Where(f => f.Route.OriginAirportId == originAirportId
                        && f.Route.DestinationAirportId == destinationAirportId
                        && f.DepartureAt >= fromUtc && f.DepartureAt < toUtc
                        && f.Status != FlightStatus.Cancelled)
            .OrderBy(f => f.DepartureAt)
            .ToList());

    public Task<Flight?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(f => f.Id == id));

    public Task<IReadOnlyList<(string FlightNumber, DateTime DepartureAt)>> GetDepartureKeysAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<(string FlightNumber, DateTime DepartureAt)>>(Items
            .Where(f => f.DepartureAt >= fromUtc && f.DepartureAt < toUtc)
            .Select(f => (f.FlightNumber, f.DepartureAt))
            .ToList());

    public Task<IReadOnlyList<DateOnly>> GetScheduledDatesAsync(int scheduleId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DateOnly>>(Items
            .Where(f => f.FlightScheduleId == scheduleId && f.ScheduledDate >= from && f.ScheduledDate <= to)
            .Select(f => f.ScheduledDate!.Value)
            .ToList());

    /// <summary>Runs just before a TryAddAsync insert, e.g. to let "another run" store the same flight first.</summary>
    public Action<Flight>? BeforeInsert { get; set; }

    /// <summary>Acts like the unique indexes on (flight number, departure) and (schedule, scheduled date).</summary>
    public async Task<bool> TryAddAsync(Flight flight, CancellationToken cancellationToken = default)
    {
        BeforeInsert?.Invoke(flight);
        var duplicate = Items.Any(f =>
            (f.FlightNumber == flight.FlightNumber && f.DepartureAt == flight.DepartureAt)
            || (flight.FlightScheduleId is not null && flight.ScheduledDate is not null
                && f.FlightScheduleId == flight.FlightScheduleId && f.ScheduledDate == flight.ScheduledDate));
        if (duplicate)
        {
            return false;
        }

        await AddAsync(flight, cancellationToken);
        return true;
    }

    public int ClearTrackingCalls { get; private set; }

    public Task<IReadOnlyList<Flight>> GetLandedOpenAsync(DateTime now, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Flight>>(Items
            .Where(f => f.ArrivalAt <= now && f.Status is not (FlightStatus.Cancelled or FlightStatus.Completed))
            .ToList());

    public void ClearTracking() => ClearTrackingCalls++;

    public Task<IReadOnlyList<Flight>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Flight>>(Items.OrderBy(f => f.DepartureAt).ToList());

    public Task<IReadOnlyList<Flight>> GetAdminPageAsync(string? search, FlightStatus? status, DateOnly? date, int skip, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Flight>>(Items
            .Where(f => status is null || f.Status == status)
            .Where(f => date is null || DateOnly.FromDateTime(f.DepartureAt) == date)
            .Where(f => string.IsNullOrWhiteSpace(search)
                        || f.FlightNumber.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                        || f.Route.Origin.City.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                        || f.Route.Destination.City.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                        || f.Aircraft.Model.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.DepartureAt).ThenBy(f => f.Id)
            .Skip(skip).Take(take)
            .ToList());

    public Task<IReadOnlyList<Flight>> GetUpcomingAsync(DateTime from, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Flight>>(Items
            .Where(f => f.DepartureAt >= from && f.Status != FlightStatus.Cancelled)
            .OrderBy(f => f.DepartureAt)
            .Take(take)
            .ToList());

    public Task<Flight?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(f => f.Id == id));

    public Task<bool> ExistsAsync(string flightNumber, DateTime departureAt, int? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(f => f.FlightNumber == flightNumber && f.DepartureAt == departureAt && f.Id != excludeId));

    public Task<bool> HasBookingsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(WithBookings.Contains(id));
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

    public Task<int> CountAvailableOnUpcomingFlightsAsync(DateTime now, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Count(s => s.Flight.DepartureAt > now && s.Flight.Status != FlightStatus.Cancelled
                                            && (s.Status == SeatStatus.Available || (s.Status == SeatStatus.Held && s.HeldUntil <= now))));

    public Task<IReadOnlyDictionary<int, int>> GetFreeSeatCountsAsync(IReadOnlyCollection<int> flightIds, CabinClass? cabinClass, DateTime now, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<int, int>>(Items
            .Where(s => flightIds.Contains(s.FlightId)
                        && (cabinClass == null || s.AircraftSeat.CabinClass == cabinClass)
                        && (s.Status == SeatStatus.Available || (s.Status == SeatStatus.Held && s.HeldUntil <= now)))
            .GroupBy(s => s.FlightId)
            .ToDictionary(g => g.Key, g => g.Count()));
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

    public IEnumerable<Booking> All => Items;

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
                leg.Id = leg.Id == 0 ? _nextId++ : leg.Id;
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
                    ticket.BookingFlightId = ticket.BookingFlight.Id;
                    ticket.FlightSeat = ticket.FlightSeatId is int seatId ? _data.Seat(seatId) : null;
                    ticket.BaggageOption = TestData.BaggageOptions.FirstOrDefault(o => o.Id == ticket.BaggageOptionId);
                    ticket.MealOption = TestData.MealOptions.FirstOrDefault(o => o.Id == ticket.MealOptionId);
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
                foreach (var refund in cancellation.Refunds)
                {
                    refund.Id = refund.Id == 0 ? _nextId++ : refund.Id;
                    refund.Cancellation = cancellation;
                    refund.Payment ??= booking.Payments.First(p => p.Id == refund.PaymentId);
                }
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

    public Task<IReadOnlyList<Booking>> GetForAdminAsync(string? pnr, string? passengerName, BookingStatus? status, int? take = null, CancellationToken cancellationToken = default)
    {
        var query = Items
            .Where(b => string.IsNullOrWhiteSpace(pnr) || b.Pnr.Contains(pnr.Trim()))
            .Where(b => string.IsNullOrWhiteSpace(passengerName)
                        || b.Passengers.Any(p => $"{p.FirstName} {p.LastName}".Contains(passengerName.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Where(b => status is null || b.Status == status)
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id);

        return Task.FromResult<IReadOnlyList<Booking>>((take is null ? query : query.Take(take.Value)).ToList());
    }

    public Task<IReadOnlyList<int>> GetUserIdsWithActiveBookingsOnFlightAsync(int flightId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<int>>(Items
            .Where(b => b.UserId != null
                        && b.Status is BookingStatus.Pending or BookingStatus.Confirmed
                        && b.Flights.Any(f => f.FlightId == flightId))
            .Select(b => b.UserId!.Value)
            .Distinct()
            .ToList());

    public Task<IReadOnlyList<Booking>> GetExpiredPendingAsync(DateTime now, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Booking>>(Items.Where(b => b.Status == BookingStatus.Pending && b.ExpiresAt <= now).ToList());

    public Task<IReadOnlyList<Booking>> GetWithOpenRefundsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Booking>>(Items
            .Where(b => b.Cancellation?.Refunds.Any(r => r.Status is RefundStatus.Pending or RefundStatus.Processing) == true)
            .ToList());

    public Task<IReadOnlyList<Booking>> GetConfirmedWithLandedFlightsAsync(DateTime now, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Booking>>(Items
            .Where(b => b.Status == BookingStatus.Confirmed && b.Flights.Any(f => f.Flight.ArrivalAt <= now))
            .ToList());

    public Task<int> CountPaidAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Count(b => b.Status is BookingStatus.Confirmed or BookingStatus.Completed));

    public Task<IReadOnlyList<Booking>> GetConfirmedDepartingBetweenAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Booking>>(Items
            .Where(b => b.Status == BookingStatus.Confirmed && b.UserId != null && b.Flights.Any(f => f.Flight.DepartureAt > from && f.Flight.DepartureAt <= to))
            .ToList());
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

internal sealed class FakeAccountEmail : IAccountEmailService
{
    /// <summary>Ids of the accounts whose address changed.</summary>
    public List<int> EmailChangedFor { get; } = new();

    public bool IsEmailConfigured => true;

    public Task<bool> SendVerificationAsync(User user, string? language, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task EmailChangedAsync(User user, string? language, CancellationToken cancellationToken = default)
    {
        EmailChangedFor.Add(user.Id);
        return Task.CompletedTask;
    }

    public Task VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ResendVerificationAsync(int userId, string? language, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<ResetTicketDto> VerifyResetCodeAsync(VerifyResetCodeRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult(new ResetTicketDto("ticket", 10));

    public Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakePaymentRepository : FakeRepository<Payment>, IPaymentRepository
{
    private readonly FakeBookingRepository _bookings;

    public FakePaymentRepository(FakeBookingRepository bookings) => _bookings = bookings;

    public Task<Payment?> GetWithBookingAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_bookings.All.SelectMany(b => b.Payments).FirstOrDefault(p => p.Id == id));

    public Task<Payment?> GetByCheckoutSessionIdAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_bookings.All.SelectMany(b => b.Payments).FirstOrDefault(p => p.StripeCheckoutSessionId == checkoutSessionId));
}

internal sealed class FakeStripeWebhookEventRepository : FakeRepository<StripeWebhookEvent>, IStripeWebhookEventRepository
{
    public IReadOnlyList<StripeWebhookEvent> Events => Items;

    public Task<StripeWebhookEvent?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(e => e.EventId == eventId));
}

/// <summary>Stands in for Stripe: hands out made-up Checkout pages, or fails when told to.</summary>
internal sealed class FakeStripeGateway : IStripeCheckoutGateway
{
    public List<StripeCheckoutRequest> Created { get; } = new();
    public List<string> Expired { get; } = new();
    public bool Fail { get; set; }

    public Task<StripeCheckoutSession> CreateSessionAsync(StripeCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new Service.Helpers.Exceptions.ServiceUnavailableException("Card payments are not available right now. Please try again later.");
        }

        Created.Add(request);
        var id = $"cs_test_{Created.Count:D4}{Guid.NewGuid():N}";
        return Task.FromResult(new StripeCheckoutSession(id, $"https://checkout.stripe.test/c/pay/{id}", request.ExpiresAtUtc));
    }

    public Task ExpireSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        Expired.Add(sessionId);
        return Task.CompletedTask;
    }
}

/// <summary>Refunds live on the bookings' cancellations, so this reads them from there like an EF query would.</summary>
internal sealed class FakeRefundRepository : FakeRepository<Refund>, IRefundRepository
{
    private readonly FakeBookingRepository _bookings;

    public FakeRefundRepository(FakeBookingRepository bookings) => _bookings = bookings;

    public IEnumerable<Refund> All => _bookings.All.Where(b => b.Cancellation is not null).SelectMany(b => b.Cancellation!.Refunds);

    public int SaveCount { get; private set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }

    public Task<Refund?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(All.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<Refund>> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Refund>>(All.Where(r => r.PaymentId == paymentId).ToList());

    public Task<Refund?> GetByProviderReferenceAsync(string providerReference, CancellationToken cancellationToken = default)
        => Task.FromResult(All.FirstOrDefault(r => r.ProviderReference == providerReference));

    public Task<IReadOnlyList<Refund>> GetPageAsync(RefundStatus? status, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Refund>>(All.Where(r => status is null || r.Status == status).OrderByDescending(r => r.Id).Take(take).ToList());

    public Task<IReadOnlyList<Refund>> GetUnsentStripeRefundsAsync(DateTime createdBefore, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Refund>>(All
            .Where(r => r.Status == RefundStatus.Pending && r.ProviderReference is null && r.Payment.Method == PaymentMethod.StripeCheckout && r.CreatedAt < createdBefore)
            .OrderBy(r => r.Id).Take(take).ToList());
}

/// <summary>
/// Stands in for Stripe's refunds: the same refund and attempt always get the same answer (like an idempotency key), every
/// distinct request is kept for the test to read, and it can be told to refuse, fail or be out of reach.
/// </summary>
internal sealed class FakeStripeRefundGateway : IStripeRefundGateway
{
    private readonly Dictionary<(int RefundId, int Attempt), StripeRefundResult> _answers = new();

    public List<StripeRefundRequest> Requests { get; } = new();

    /// <summary>Every call, including repeats of the same refund and attempt.</summary>
    public int Calls { get; private set; }

    public bool Unavailable { get; set; }

    /// <summary>What a new refund answers; "succeeded" by default.</summary>
    public Func<StripeRefundRequest, StripeRefundResult> Answer { get; set; } = request => new StripeRefundResult($"re_test_{request.RefundId}_{request.Attempt}", "succeeded", null);

    public Task<StripeRefundResult> CreateRefundAsync(StripeRefundRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Unavailable)
        {
            throw new Service.Helpers.Exceptions.ServiceUnavailableException("Refunds are not available right now. Please try again later.");
        }

        if (!_answers.TryGetValue((request.RefundId, request.Attempt), out var result))
        {
            Requests.Add(request);
            result = Answer(request);
            _answers[(request.RefundId, request.Attempt)] = result;
        }

        return Task.FromResult(result);
    }
}
