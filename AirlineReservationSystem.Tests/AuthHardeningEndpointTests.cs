using System.Net;
using System.Net.Http.Json;
using System.Text;
using Service.Helpers.DTOs.Account;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>Over HTTP: nobody registers as an Admin, sign-in is paused after repeated failures, and the sign-in endpoints are limited per IP address.</summary>
public class AuthHardeningEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthHardeningEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_IgnoresARoleSentInTheRequest_AndCreatesAClient()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("sneaky");
        var body = $$"""
            { "firstName": "Sneaky", "lastName": "User", "email": "{{email}}", "password": "secret1", "confirmPassword": "secret1",
              "role": "Admin", "isActive": true, "Role": 1 }
            """;

        var response = await client.PostAsync("/api/auth/register", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registered = await response.ReadAsync<RegisterResponseDto>();
        Assert.Equal("Client", registered.User.Role);

        // Signing in gives a Client token: the Admin area refuses it.
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = "secret1" }, Json);
        var token = (await login.ReadAsync<AuthResponseDto>()).AccessToken;
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/admin/dashboard")).StatusCode);
    }

    [Fact]
    public async Task FiveWrongPasswords_PauseSignIn_With429AndRetryAfter()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("locked");
        await client.RegisterAsync(email);

        for (var i = 0; i < 5; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = "wrong-password" }, Json);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        // Even the right password is refused during the pause.
        var paused = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = "secret1" }, Json);

        Assert.Equal(HttpStatusCode.TooManyRequests, paused.StatusCode);
        Assert.Equal("application/problem+json", paused.Content.Headers.ContentType?.MediaType);
        Assert.Equal(900, int.Parse(paused.Headers.GetValues("Retry-After").Single()));
        Assert.Contains("Try again", (await paused.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TheSignInEndpoints_AreLimitedPerIpAddress_WhileOtherEndpointsAreNot()
    {
        // A factory of its own (its own throwaway database, dropped at the end) with a request limit of 3 per minute.
        var limited = new ApiFactory { RateLimitPerMinute = 3 };
        await limited.InitializeAsync();

        try
        {
            var client = limited.CreateClient();

            for (var i = 0; i < 3; i++)
            {
                var attempt = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = UniqueEmail("rate"), Password = "wrong-password" }, Json);
                Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
            }

            var refused = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = UniqueEmail("rate"), Password = "wrong-password" }, Json);
            Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
            Assert.Equal("60", refused.Headers.GetValues("Retry-After").Single());

            // Register shares the same limit; the rest of the API is untouched.
            var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
            {
                FirstName = "A", LastName = "B", Email = UniqueEmail("rate"), Password = "secret1", ConfirmPassword = "secret1"
            }, Json);
            Assert.Equal(HttpStatusCode.TooManyRequests, register.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/airports")).StatusCode);
        }
        finally
        {
            await ((IAsyncLifetime)limited).DisposeAsync();
        }
    }

    [Fact]
    public async Task Swagger_DescribesThe429OfSignIn()
    {
        var swagger = await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");

        using var document = System.Text.Json.JsonDocument.Parse(swagger);
        var login = document.RootElement.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post").GetProperty("responses");
        Assert.True(login.TryGetProperty("429", out _));
    }
}
