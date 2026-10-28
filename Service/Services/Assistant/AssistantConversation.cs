using Service.Helpers.DTOs.Assistant;

namespace Service.Services;

/// <summary>Turns what the browser sent into the conversation the provider is given.</summary>
public static class AssistantConversation
{
    /// <summary>The messages as sent, with the single <see cref="AssistantChatRequestDto.Message"/> added at the end when there is one.</summary>
    public static List<AssistantMessageDto> All(AssistantChatRequestDto request)
    {
        var all = (request.Messages ?? new List<AssistantMessageDto>()).Where(m => m is not null).ToList();
        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            all.Add(new AssistantMessageDto { Role = "user", Content = request.Message });
        }

        return all;
    }

    /// <summary>
    /// Providers want the two voices to take turns, starting with the traveller. Texts are trimmed, two messages in a row from the
    /// same side become one, and answers that stand before the first question are dropped.
    /// </summary>
    public static List<AssistantTurn> Normalize(IEnumerable<AssistantMessageDto> messages)
    {
        var turns = new List<AssistantTurn>();
        foreach (var message in messages)
        {
            var text = (message.Content ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                continue;
            }

            var role = message.Role == "assistant" ? "assistant" : "user";
            if (turns.Count == 0 && role == "assistant")
            {
                continue;
            }

            if (turns.Count > 0 && turns[^1].Role == role)
            {
                turns[^1] = turns[^1] with { Text = turns[^1].Text + "\n\n" + text };
            }
            else
            {
                turns.Add(new AssistantTurn(role, text));
            }
        }

        return turns;
    }
}

public record AssistantTurn(string Role, string Text);
