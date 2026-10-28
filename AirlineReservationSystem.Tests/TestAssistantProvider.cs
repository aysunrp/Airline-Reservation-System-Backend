using Service.Services;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Tests;

/// <summary>Stands in for the AI provider in API tests: remembers the questions, answers what the test says, never calls out.</summary>
public sealed class TestAssistantProvider : IAssistantProvider
{
    private readonly object _lock = new();
    private readonly List<AssistantCompletionRequest> _requests = new();

    public bool IsConfigured { get; set; } = true;

    public Func<AssistantCompletionRequest, string> Answer { get; set; } = _ => "Economy is the most affordable cabin.";

    public Exception? Throw { get; set; }

    public IReadOnlyList<AssistantCompletionRequest> Requests
    {
        get { lock (_lock) { return _requests.ToList(); } }
    }

    public void Reset()
    {
        lock (_lock) { _requests.Clear(); }
        IsConfigured = true;
        Throw = null;
        Answer = _ => "Economy is the most affordable cabin.";
    }

    public Task<string> CompleteAsync(AssistantCompletionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock) { _requests.Add(request); }
        return Throw is null ? Task.FromResult(Answer(request)) : Task.FromException<string>(Throw);
    }
}
