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
/// Ask AEROVA's answers from the Anthropic Messages API (one HTTPS request per question, nothing is stored there by AEROVA). The key
/// is sent only in a request header; it is never logged, returned or put in an exception. Only the HTTP status and the time taken are
/// logged: never the conversation, and never the body of the provider's answer or of an error.
/// </summary>
public class AnthropicAssistantProvider : IAssistantProvider
{
    public const string UnavailableMessage = "The assistant is not available right now. Please try again later.";

    private readonly HttpClient _http;
    private readonly TravelAssistantSettings _settings;
    private readonly ILogger<AnthropicAssistantProvider> _logger;

    public AnthropicAssistantProvider(HttpClient http, IOptions<TravelAssistantSettings> settings, ILogger<AnthropicAssistantProvider> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured => _settings.IsConfigured;

    public async Task<string> CompleteAsync(AssistantCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        var body = new
        {
            model = _settings.Model,
            max_tokens = Math.Clamp(request.MaxOutputTokens, 50, 4000),
            system = request.System,
            messages = request.Turns.Select(t => new { role = t.Role, content = t.Text }).ToArray()
        };

        // Sent with a known length (not chunked): proxies and simple servers in front of the provider handle that best.
        using var message = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        message.Headers.Add("x-api-key", _settings.ApiKey);
        message.Headers.Add("anthropic-version", _settings.ApiVersion);

        // The provider gets a fixed time; a traveller who closes the page cancels the request on top of that.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _settings.TimeoutSeconds)));

        var started = DateTime.UtcNow;
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(message, HttpCompletionOption.ResponseContentRead, timeout.Token);
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

        using (response)
        {
            var elapsed = (int)(DateTime.UtcNow - started).TotalMilliseconds;
            if (!response.IsSuccessStatusCode)
            {
                var code = await RefusalCodeAsync(response, elapsed, timeout.Token);
                throw new ServiceUnavailableException(UnavailableMessage, code);
            }

            string text;
            try
            {
                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
                text = ReadText(document.RootElement);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw new GatewayTimeoutException("The assistant took too long to answer. Please try again.");
            }
            catch (JsonException)
            {
                _logger.LogWarning("The assistant failed: {Code}. {Hint} (the answer is not JSON)", AssistantErrorCodes.BadAnswer, AssistantErrorCodes.Describe(AssistantErrorCodes.BadAnswer));
                throw new ServiceUnavailableException(UnavailableMessage, AssistantErrorCodes.BadAnswer);
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

    /// <summary>The text blocks of the answer, one after the other.</summary>
    internal static string ReadText(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object
                && block.TryGetProperty("type", out var type) && type.GetString() == "text"
                && block.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String)
            {
                text.Append(value.GetString());
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Turns a refusal into a code and writes it, with the HTTP status and the provider's error kind, to the log. The provider's own text is
    /// read only to recognise a billing problem; it is never logged or returned (it could quote the request).
    /// </summary>
    private async Task<string> RefusalCodeAsync(HttpResponseMessage response, int elapsed, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var (kind, billing) = await ReadErrorAsync(response, cancellationToken);

        var code = billing ? AssistantErrorCodes.Billing
            : response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? AssistantErrorCodes.KeyRejected
            : response.StatusCode == HttpStatusCode.NotFound ? AssistantErrorCodes.ModelUnavailable
            : response.StatusCode == HttpStatusCode.PaymentRequired ? AssistantErrorCodes.Billing
            : response.StatusCode == HttpStatusCode.TooManyRequests || status == 529 ? AssistantErrorCodes.Busy
            : status >= 500 ? AssistantErrorCodes.ProviderError
            : AssistantErrorCodes.RequestRejected;

        var message = "The assistant failed: {Code}. {Hint} (provider status {Status}, error kind {Kind}, {Milliseconds} ms)";
        if (code is AssistantErrorCodes.KeyRejected or AssistantErrorCodes.ModelUnavailable or AssistantErrorCodes.Billing or AssistantErrorCodes.RequestRejected)
        {
            _logger.LogError(message, code, AssistantErrorCodes.Describe(code), status, kind, elapsed);
        }
        else
        {
            _logger.LogWarning(message, code, AssistantErrorCodes.Describe(code), status, kind, elapsed);
        }

        return code;
    }

    /// <summary>The provider's error kind (for example "authentication_error", letters and underscores only) and whether it is a billing problem.</summary>
    private static async Task<(string Kind, bool Billing)> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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
                return ("unknown", false);
            }

            var kind = error.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String ? type.GetString() ?? string.Empty : string.Empty;
            kind = kind.Length is > 0 and <= 40 && kind.All(c => c is (>= 'a' and <= 'z') or '_') ? kind : "unknown";

            var text = error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String ? message.GetString() ?? string.Empty : string.Empty;
            var billing = text.Contains("credit balance", StringComparison.OrdinalIgnoreCase) || text.Contains("billing", StringComparison.OrdinalIgnoreCase);
            return (kind, billing);
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException or InvalidOperationException)
        {
            return ("unknown", false);
        }
    }
}
