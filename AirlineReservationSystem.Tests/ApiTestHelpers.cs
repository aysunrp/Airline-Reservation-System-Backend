using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Service.Helpers.DTOs.Account;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Tests;

internal static class ApiTestHelpers
{
    /// <summary>Same JSON shape as the API: camelCase and enums as text.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string UniqueEmail(string name = "user") => $"{name}.{Guid.NewGuid():N}@example.com";

    /// <summary>Registers a new client account, signs it in (registration itself does not) and returns its token and user.</summary>
    public static async Task<AuthResponseDto> RegisterAsync(this HttpClient client, string? email = null, string password = "secret1")
    {
        email ??= UniqueEmail();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FirstName = "Aysun",
            LastName = "Panahli",
            Email = email,
            Password = password,
            ConfirmPassword = password
        }, Json);
        response.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = password }, Json);
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<AuthResponseDto>(Json))!;
    }

    /// <summary>
    /// There is no public way to become an admin, so the test writes an Admin account straight into the
    /// test database and signs in through the API like the admin panel would.
    /// </summary>
    public static async Task<HttpClient> CreateAdminClientAsync(this ApiFactory factory)
        => (await factory.CreateAccountAsync(UserRole.Admin)).Client;

    /// <summary>For the admin actions only a SuperAdmin may do: deleting, generating flights, changing the cancellation rules.</summary>
    public static async Task<HttpClient> CreateSuperAdminClientAsync(this ApiFactory factory)
        => (await factory.CreateAccountAsync(UserRole.SuperAdmin)).Client;

    /// <summary>Writes an account with the given role straight into the test database and signs it in through the API.</summary>
    public static async Task<TestAccount> CreateAccountAsync(this ApiFactory factory, UserRole role)
    {
        const string password = "admin-pass";
        var email = UniqueEmail(role.ToString().ToLowerInvariant());
        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new User { Email = email, PasswordHash = hasher.Hash(password), FirstName = "Aerova", LastName = role.ToString(), Role = role };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            id = user.Id;
        }

        var client = factory.CreateClient();
        var token = await client.SignInAsync(email, password);
        return new TestAccount(client.WithToken(token), id, email, password, token);
    }

    /// <summary>Signs in and returns the access token (the sign-in must succeed).</summary>
    public static async Task<string> SignInAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = password }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<AuthResponseDto>()).AccessToken;
    }

    /// <summary>A client that sends the given token.</summary>
    public static HttpClient ClientWith(this ApiFactory factory, string token) => factory.CreateClient().WithToken(token);

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

/// <summary>An account made for a test and the client that is signed in as it.</summary>
internal sealed record TestAccount(HttpClient Client, int Id, string Email, string Password, string Token);
