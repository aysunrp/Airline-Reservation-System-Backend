using FluentValidation;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Assistant;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Helpers.Validators;

/// <summary>
/// What one question to Ask AEROVA may look like. The limits come from TravelAssistant settings and bound the cost of a request:
/// a message length, a number of messages and a total length. Roles are only "user" and "assistant" (no "system"), and the conversation
/// must end with a question from the traveller.
/// </summary>
public class AssistantChatRequestValidator : AbstractValidator<AssistantChatRequestDto>
{
    public static readonly string[] Roles = { "user", "assistant" };

    public AssistantChatRequestValidator(IOptions<TravelAssistantSettings> options)
    {
        var settings = options.Value;

        RuleFor(x => x.Language).MaximumLength(10);

        RuleFor(x => x.Message).MaximumLength(settings.MaxMessageLength).WithMessage($"A message can have at most {settings.MaxMessageLength} characters.");

        RuleFor(x => x.Messages).NotNull().WithMessage("Send the conversation as a list of messages.");

        RuleFor(x => x).Custom((request, context) =>
        {
            var all = AssistantConversation.All(request);

            if (all.Count == 0)
            {
                context.AddFailure("Messages", "Type a message.");
                return;
            }

            if (all.Count > settings.MaxMessages)
            {
                context.AddFailure("Messages", $"At most {settings.MaxMessages} messages can be sent at once.");
            }

            var total = 0;
            for (var index = 0; index < all.Count; index++)
            {
                var message = all[index];
                if (!Roles.Contains(message.Role))
                {
                    context.AddFailure($"Messages[{index}].Role", "A message is either from the \"user\" or from the \"assistant\".");
                }

                var length = (message.Content ?? string.Empty).Trim().Length;
                if (length == 0)
                {
                    context.AddFailure($"Messages[{index}].Content", "A message cannot be empty.");
                }
                else if (length > settings.MaxMessageLength)
                {
                    context.AddFailure($"Messages[{index}].Content", $"A message can have at most {settings.MaxMessageLength} characters.");
                }

                total += length;
            }

            if (total > settings.MaxTotalLength)
            {
                context.AddFailure("Messages", $"The conversation is too long: at most {settings.MaxTotalLength} characters in all.");
            }

            if (all[^1].Role != "user")
            {
                context.AddFailure("Messages", "The conversation must end with your question.");
            }
        });
    }
}
