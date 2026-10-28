using Service.Services;

namespace Service.Services.Interfaces;

/// <summary>What the provider is asked: the rules (system), the conversation so far and how long the answer may be.</summary>
public record AssistantCompletionRequest(string System, IReadOnlyList<AssistantTurn> Turns, int MaxOutputTokens);

/// <summary>
/// The AI provider behind Ask AEROVA. One implementation talks to the real provider; tests use a fake. An implementation throws
/// <see cref="Helpers.Exceptions.ServiceUnavailableException"/> when the provider is not set up, refuses or fails, and
/// <see cref="Helpers.Exceptions.GatewayTimeoutException"/> when it does not answer in time; the caller's own cancellation is never swallowed.
/// </summary>
public interface IAssistantProvider
{
    /// <summary>True when the provider has what it needs (a key and a supported provider name).</summary>
    bool IsConfigured { get; }

    Task<string> CompleteAsync(AssistantCompletionRequest request, CancellationToken cancellationToken = default);
}
