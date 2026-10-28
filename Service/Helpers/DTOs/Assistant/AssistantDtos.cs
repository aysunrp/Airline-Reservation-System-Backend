namespace Service.Helpers.DTOs.Assistant;

/// <summary>One turn of the conversation as the browser keeps it. Only "user" and "assistant" are accepted: there is no "system" role.</summary>
public class AssistantMessageDto
{
    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// A question to Ask AEROVA. <see cref="Messages"/> is the recent conversation, oldest first, ending with the question to answer;
/// <see cref="Message"/> may be used instead to send just one question (it is added at the end). The history is context only: it is
/// untrusted and can never change the rules the server sets.
/// </summary>
public class AssistantChatRequestDto
{
    /// <summary>The page language (en, az, ru); the answer follows the language the traveller writes in, this is the fallback.</summary>
    public string? Language { get; set; }

    public List<AssistantMessageDto> Messages { get; set; } = new();

    public string? Message { get; set; }
}

public record AssistantChatResponseDto(string Reply);
