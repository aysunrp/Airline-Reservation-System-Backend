using Domain.Entities;
using Repository.Repositories.Interfaces;

namespace Service.Tests;

internal sealed class FakeRouteRepository : FakeRepository<Route>, IRouteRepository
{
    public FakeRouteRepository(params Route[] items) : base(items)
    {
    }

    public HashSet<int> InUse { get; } = new();

    public Task<IReadOnlyList<Route>> GetAllWithAirportsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Route>>(Items.OrderBy(r => r.Origin.Code).ThenBy(r => r.Destination.Code).ToList());

    public Task<Route?> GetWithAirportsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

    public Task<Route?> GetByAirportsAsync(int originAirportId, int destinationAirportId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(r => r.Origin.Id == originAirportId && r.Destination.Id == destinationAirportId));

    public Task<bool> ExistsAsync(int originAirportId, int destinationAirportId, int? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(r => r.Origin.Id == originAirportId && r.Destination.Id == destinationAirportId && r.Id != excludeId));

    public Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(InUse.Contains(id));
}

internal sealed class FakeFlightScheduleRepository : FakeRepository<FlightSchedule>, IFlightScheduleRepository
{
    public FakeFlightScheduleRepository(params FlightSchedule[] items) : base(items)
    {
    }

    public Task<IReadOnlyList<FlightSchedule>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FlightSchedule>>(Items.OrderBy(s => s.FlightNumber).ToList());

    public Task<FlightSchedule?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));
}

/// <summary>Reads booked passengers straight from the fake booking store.</summary>
internal sealed class FakeBookingPassengerRepository : FakeRepository<BookingPassenger>, IBookingPassengerRepository
{
    private readonly FakeBookingRepository _bookings;

    public FakeBookingPassengerRepository(FakeBookingRepository bookings) => _bookings = bookings;

    public async Task<IReadOnlyList<BookingPassenger>> GetAllWithBookingsAsync(CancellationToken cancellationToken = default)
        => (await _bookings.GetAllAsync(cancellationToken)).SelectMany(b => b.Passengers).ToList();
}
