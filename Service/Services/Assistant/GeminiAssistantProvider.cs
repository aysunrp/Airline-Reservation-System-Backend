using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Ask AEROVA's answers from the Google Gemini API (<c>models/{model}:generateContent</c>, one HTTPS request per question; AEROVA stores
/// nothing there). The key is sent only in the <c>x-goog-api-key</c> header, never in the address, so it cannot end up in a URL log. It is
/// never logged, returned or put in an exception. Only the HTTP status, the error kind and the time taken are logged: never the
/// conversation, and never the text of the provider's answer or of an error.
/// </summary>
public class GeminiAssistantProvider : IAssistantProvider
{
    public const string UnavailableMessage = "The assistant is not available right now. Please try again later.";

    private readonly HttpClient _http;
    private readonly TravelAssistantSettings _settings;
    private readonly ILogger<GeminiAssistantProvider> _logger;

    public GeminiAssistantProvider(HttpClient http, IOptions<TravelAssistantSettings> settings, ILogger<GeminiAssistantProvider> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured => _settings.IsConfigured;

    public async Task<string> CompleteAsync(AssistantCompletionRequest request, CancellationToken cancellationToken = default)
    {
        RequireUsableKey();

        // Gemini names the two voices "user" and "model"; the system rules go in systemInstruction, never in the conversation.
        var generationConfig = new Dictionary<string, object?>
        {
            ["maxOutputTokens"] = Math.Clamp(request.MaxOutputTokens, 50, 8000),
            ["temperature"] = 0.4
        };
        if (_settings.ThinkingBudget is int budget)
        {
            // Models that think spend output tokens on it first; a small answer limit would be used up before the answer starts.
            generationConfig["thinkingConfig"] = new { thinkingBudget = budget };
        }

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = request.System } } },
            contents = request.Turns.Select(t => new { role = t.Role == "assistant" ? "model" : "user", parts = new[] { new { text = t.Text } } }).ToArray(),
            generationConfig
        };

        using var message = CreateRequest(HttpMethod.Post, $"models/{Uri.EscapeDataString(_settings.Model)}:generateContent", JsonSerializer.Serialize(body));

        var (response, elapsed) = await SendAsync(message, cancellationToken);
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ServiceUnavailableException(UnavailableMessage, await RefusalCodeAsync(response, elapsed, cancellationToken));
            }

            string text;
            string? blocked;
            try
            {
                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                (text, blocked) = ReadAnswer(document.RootElement);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (JsonException)
            {
                _logger.LogWarning("The assistant failed: {Code}. {Hint} (the answer is not JSON)", AssistantErrorCodes.BadAnswer, AssistantErrorCodes.Describe(AssistantErrorCodes.BadAnswer));
                throw new ServiceUnavailableException(UnavailableMessage, AssistantErrorCodes.BadAnswer);
            }

            if (blocked is not null)
            {
                _logger.LogWarning("The assistant failed: {Code}. {Hint} (reason {Reason})", AssistantErrorCodes.Blocked, AssistantErrorCodes.Describe(AssistantErrorCodes.Blocked), blocked);
                throw new ServiceUnavailableException(UnavailableMessage, AssistantErrorCodes.Blocked);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("The assistant failed: {Code}. {Hint} (no text in the answer)", AssistantErrorCodes.BadAnswer, AssistantErrorCodes.Describe(AssistantErrorCodes.BadAnswer));
                throw new ServiceUnavailableException(UnavailableMessage, AssistantErrorCodes.BadAnswer);
            }

            _logger.LogInformation("The assistant provider answered in {Milliseconds} ms.", elapsed);
            return text.Trim();
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        RequireUsableKey();

        using var message = CreateRequest(HttpMethod.Get, "models?pageSize=200", null);
        var (response, elapsed) = await SendAsync(message, cancellationToken);
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ServiceUnavailableException(UnavailableMessage, await RefusalCodeAsync(response, elapsed, cancellationToken));
            }

            try
            {
                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                return ReadModels(document.RootElement);
            }
            catch (JsonException)
            {
                throw new ServiceUnavailableException(UnavailableMessage, AssistantErrorCodes.BadAnswer);
            }
        }
    }

    /// <summary>A stored placeholder, another provider's key or a bad paste can never be sent: say so, instead of a vague network error or a key sent to the wrong company.</summary>
    private void RequireUsableKey()
    {
        if (!IsConfigured)
        {
            throw new ServiceUnavailableException(UnavailableMessage, AssistantErrorCodes.NotConfigured);
        }

        if (!_settings.HasWellFormedKey)
        {
            _logger.LogError("The assistant failed: {Code}. {Hint} (the stored key has {Length} characters)",
                AssistantErrorCodes.KeyMalformed, AssistantErrorCodes.Describe(AssistantErrorCodes.KeyMalformed), _settings.TrimmedApiKey.Length);
            throw new ServiceUnavailableException(UnavailableMessage, AssistantErrorCodes.KeyMalformed);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? json)
    {
        var message = new HttpRequestMessage(method, _settings.Endpoint.TrimEnd('/') + "/" + path);
        message.Headers.Add("x-goog-api-key", _settings.TrimmedApiKey);
        if (json is not null)
        {
            // Sent with a known length (not chunked): proxies and simple servers in front of a provider handle that best.
            message.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return message;
    }

    /// <summary>One request with the provider's time limit; a slow provider is a 504, the caller's own cancellation is never swallowed.</summary>
    private async Task<(HttpResponseMessage Response, int Elapsed)> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _settings.TimeoutSeconds)));

        var started = DateTime.UtcNow;
        try
        {
            var response = await _http.SendAsync(message, HttpCompletionOption.ResponseContentRead, timeout.Token);
            return (response, (int)(DateTime.UtcNow - started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("The assistant provider did not answer within {Seconds} seconds.", _settings.TimeoutSeconds);
            throw new GatewayTimeoutException("The assistant took too long to answer. Please try again.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("The assistant failed: {Code}. {Hint} ({Error})", AssistantErrorCodes.Unreachable, AssistantErrorCodes.Describe(AssistantErrorCodes.Unreachable), exception.HttpRequestError);
            throw new ServiceUnavailableException(UnavailableMessage, AssistantErrorCodes.Unreachable);
        }
    }

    /// <summary>The text of the first candidate (parts the model marked as its own thinking are left out), and why nothing can be shown when it was blocked.</summary>
    internal static (string Text, string? BlockedReason) ReadAnswer(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return (string.Empty, null);
        }

        if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.ValueKind == JsonValueKind.Object
            && feedback.TryGetProperty("blockReason", out var block) && block.ValueKind == JsonValueKind.String)
        {
            return (string.Empty, Plain(block.GetString()));
        }

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
        {
            return (string.Empty, null);
        }

        var candidate = candidates[0];
        var text = new StringBuilder();
        if (candidate.ValueKind == JsonValueKind.Object && candidate.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object
            && content.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Object
                    && !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                    && part.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String)
                {
                    text.Append(value.GetString());
                }
            }
        }

        // A safety stop with nothing written is a block; any other stop (including MAX_TOKENS) with text shows what was written.
        if (text.Length == 0 && candidate.ValueKind == JsonValueKind.Object && candidate.TryGetProperty("finishReason", out var finish) && finish.ValueKind == JsonValueKind.String
            && Plain(finish.GetString()) is { } reason && reason is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII" or "RECITATION")
        {
            return (string.Empty, reason);
        }

        return (text.ToString(), null);
    }

    /// <summary>The names of the models that can write answers (generateContent), without the "models/" prefix.</summary>
    internal static IReadOnlyList<string> ReadModels(JsonElement root)
    {
        var names = new List<string>();
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
        {
            foreach (var model in models.EnumerateArray())
            {
                if (model.ValueKind != JsonValueKind.Object || !model.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var canWrite = model.TryGetProperty("supportedGenerationMethods", out var methods) && methods.ValueKind == JsonValueKind.Array
                    && methods.EnumerateArray().Any(m => m.ValueKind == JsonValueKind.String && m.GetString() == "generateContent");
                if (canWrite)
                {
                    names.Add((name.GetString() ?? string.Empty).Replace("models/", string.Empty));
                }
            }
        }

        return names.OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    /// <summary>Turns a refusal into a code and writes it, with the HTTP status and the provider's error kind, to the log. The provider's text is never logged.</summary>
    private async Task<string> RefusalCodeAsync(HttpResponseMessage response, int elapsed, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var (kind, keyInvalid, quota) = await ReadErrorAsync(response, cancellationToken);

        var code = keyInvalid || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? AssistantErrorCodes.KeyRejected
            : response.StatusCode == HttpStatusCode.NotFound ? AssistantErrorCodes.ModelUnavailable
            : quota || response.StatusCode == HttpStatusCode.TooManyRequests || status == 529 ? AssistantErrorCodes.Busy
            : status >= 500 ? AssistantErrorCodes.ProviderError
            : AssistantErrorCodes.RequestRejected;

        var message = "The assistant failed: {Code}. {Hint} (provider status {Status}, error kind {Kind}, {Milliseconds} ms)";
        if (code is AssistantErrorCodes.KeyRejected or AssistantErrorCodes.ModelUnavailable or AssistantErrorCodes.RequestRejected)
        {
            _logger.LogError(message, code, AssistantErrorCodes.Describe(code), status, kind, elapsed);
        }
        else
        {
            _logger.LogWarning(message, code, AssistantErrorCodes.Describe(code), status, kind, elapsed);
        }

        return code;
    }

    /// <summary>
    /// The provider's error kind (for example "INVALID_ARGUMENT", capital letters and underscores only), whether it says the key is not valid,
    /// and whether it is a quota problem. The provider's own message is read only to recognise those, never logged or returned.
    /// </summary>
    private static async Task<(string Kind, bool KeyInvalid, bool Quota)> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (body.Length > 8192)
            {
                body = body[..8192];
            }

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
            {
                return ("unknown", false, false);
            }

            var kind = error.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String ? status.GetString() ?? string.Empty : string.Empty;
            kind = kind.Length is > 0 and <= 40 && kind.All(c => c is (>= 'A' and <= 'Z') or '_') ? kind : "unknown";

            var text = error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String ? message.GetString() ?? string.Empty : string.Empty;
            var keyInvalid = text.Contains("API key not valid", StringComparison.OrdinalIgnoreCase) || text.Contains("API key expired", StringComparison.OrdinalIgnoreCase)
                || (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array
                    && details.EnumerateArray().Any(d => d.ValueKind == JsonValueKind.Object && d.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                                                         && reason.GetString() is "API_KEY_INVALID" or "API_KEY_EXPIRED" or "API_KEY_SERVICE_BLOCKED"));
            var quota = kind == "RESOURCE_EXHAUSTED" || text.Contains("quota", StringComparison.OrdinalIgnoreCase);
            return (kind, keyInvalid, quota);
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException or InvalidOperationException)
        {
            return ("unknown", false, false);
        }
    }

    private static string? Plain(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Length <= 40 && value.All(c => c is (>= 'A' and <= 'Z') or '_') ? value : "OTHER";
}
