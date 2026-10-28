namespace Service.Services.Interfaces;

/// <summary>
/// The facts about AEROVA the assistant may state as verified, read-only: they come from the configuration AEROVA itself uses (fare rules,
/// baggage and meal options). Nothing about a person, a booking or a payment is ever in it.
/// </summary>
public interface IAssistantKnowledge
{
    /// <summary>The facts as plain text for the assistant's instructions; empty when they could not be read (the assistant then says it cannot verify them).</summary>
    Task<string> GetFactsAsync(CancellationToken cancellationToken = default);
}
