namespace Service.Services.Interfaces;

public sealed record TripCompletionResult(int Flights, int Tickets, int Bookings);

public interface ITripCompletionService
{
    /// <summary>Marks landed flights Completed, their issued tickets Used, and bookings whose every flight has landed Completed.</summary>
    Task<TripCompletionResult> CompleteFinishedAsync(CancellationToken cancellationToken = default);
}
