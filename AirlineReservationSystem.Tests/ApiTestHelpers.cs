using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Service.Helpers.DTOs.Account;

namespace AirlineReservationSystem.Tests;

internal static class ApiTestHelpers
{
    /// <summary>Same JSON shape as the API: camelCase and enums as text.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string UniqueEmail(string name = "user") => $"{name}.{Guid.NewGuid():N}@example.com";

    /// <summary>Registers a new client account and returns its token and user.</summary>
    public static async Task<AuthResponseDto> RegisterAsync(this HttpClient client, string? email = null, string password = "secret1")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FirstName = "Aysun",
            LastName = "Panahli",
            Email = email ?? UniqueEmail(),
            Password = password,
            ConfirmPassword = password
        }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>(Json))!;
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
        => (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement;
}
