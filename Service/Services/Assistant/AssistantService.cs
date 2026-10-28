using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Assistant;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AssistantService : IAssistantService
{
    private readonly IAssistantProvider _provider;
    private readonly IAssistantKnowledge _knowledge;
    private readonly IValidator<AssistantChatRequestDto> _validator;
    private readonly TravelAssistantSettings _settings;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        IAssistantProvider provider,
        IAssistantKnowledge knowledge,
        IValidator<AssistantChatRequestDto> validator,
        IOptions<TravelAssistantSettings> settings,
        ILogger<AssistantService> logger)
    {
        _logger = logger;
        _provider = provider;
        _knowledge = knowledge;
        _validator = validator;
        _settings = settings.Value;
    }

    public async Task<AssistantChatResponseDto> ChatAsync(AssistantChatRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        // Nothing is spent on a question when the assistant cannot answer it.
        if (!_provider.IsConfigured)
        {
            WarnNotConfigured();
            throw new ServiceUnavailableException(GeminiAssistantProvider.UnavailableMessage, AssistantErrorCodes.NotConfigured);
        }

        var turns = AssistantConversation.Normalize(AssistantConversation.All(request));
        var system = AssistantPrompt.Build(request.Language, await _knowledge.GetFactsAsync(cancellationToken));

        try
        {
            var reply = await _provider.CompleteAsync(new AssistantCompletionRequest(system, turns, _settings.MaxOutputTokens), cancellationToken);
            return new AssistantChatResponseDto(reply);
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or ServiceUnavailableException or GatewayTimeoutException))
        {
            // Whatever the provider code did wrong, the traveller gets the same safe answer; only the kind of error is logged, never its text.
            _logger.LogError("The assistant failed unexpectedly ({ErrorType}).", exception.GetType().Name);
            throw new ServiceUnavailableException(GeminiAssistantProvider.UnavailableMessage);
        }
    }

    /// <summary>Says in the log that no question can be answered and what to set (never the key itself). Questions are limited per address, so this is not a flood.</summary>
    private void WarnNotConfigured()
    {
        _logger.LogWarning("The assistant failed: {Code}. {Hint} (provider \"{Provider}\", key {KeyState})",
            AssistantErrorCodes.NotConfigured, AssistantErrorCodes.Describe(AssistantErrorCodes.NotConfigured), _settings.Provider,
            string.IsNullOrWhiteSpace(_settings.ApiKey) ? "missing" : "present");
    }
}
