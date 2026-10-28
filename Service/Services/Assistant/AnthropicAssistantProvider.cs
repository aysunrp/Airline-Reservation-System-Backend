using System.Net;
using System.Net.Http.Json;
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

        using var message = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint) { Content = JsonContent.Create(body) };
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
        catch (HttpRequestException)
        {
            _logger.LogWarning("The assistant provider could not be reached.");
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        using (response)
        {
            var elapsed = (int)(DateTime.UtcNow - started).TotalMilliseconds;
            if (!response.IsSuccessStatusCode)
            {
                LogRefusal(response.StatusCode, elapsed);
                throw new ServiceUnavailableException(UnavailableMessage);
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
                _logger.LogWarning("The assistant provider answered with something that is not understood.");
                throw new ServiceUnavailableException(UnavailableMessage);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("The assistant provider answered without any text.");
                throw new ServiceUnavailableException(UnavailableMessage);
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

    private void LogRefusal(HttpStatusCode status, int elapsed)
    {
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogError("The assistant provider refused the credentials ({Status}); check TravelAssistant:ApiKey.", (int)status);
        }
        else if (status == HttpStatusCode.TooManyRequests || (int)status == 529)
        {
            _logger.LogWarning("The assistant provider is busy or its limit is reached ({Status}, {Milliseconds} ms).", (int)status, elapsed);
        }
        else
        {
            _logger.LogWarning("The assistant provider answered {Status} ({Milliseconds} ms).", (int)status, elapsed);
        }
    }
}
