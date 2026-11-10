using Service.Helpers.DTOs.Assistant;

namespace Service.Services.Interfaces;

/// <summary>Ask AEROVA: answers a traveller's question with the AI provider, inside rules the server sets.</summary>
public interface IAssistantService
{
    /// <summary>
    /// Answers the last question of the conversation. 400 for a message that is empty, too long, from the wrong side or too many; 503 when
    /// the assistant is not set up or the provider fails; 504 when the provider is too slow. The conversation is never stored.
    /// </summary>
    Task<AssistantChatResponseDto> ChatAsync(AssistantChatRequestDto request, CancellationToken cancellationToken = default);
}
