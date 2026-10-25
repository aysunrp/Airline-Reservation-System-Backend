using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Passenger;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

public class AuthAndProfileEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthAndProfileEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_Returns201WithoutAToken_AndLoginWorks()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("aysun");

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FirstName = "Aysun", LastName = "Panahli", Email = email, Password = "secret1", ConfirmPassword = "secret1"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.ReadJsonAsync();
        Assert.Equal("User", registered.GetProperty("user").GetProperty("role").GetString());
        Assert.False(registered.GetProperty("user").GetProperty("emailVerified").GetBoolean());
        // The person is not signed in by registering.
        Assert.False(registered.TryGetProperty("accessToken", out _));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email.ToUpperInvariant(), Password = "secret1" }, Json);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(registered.GetProperty("user").GetProperty("id").GetInt32(), (await loginResponse.ReadAsync<AuthResponseDto>()).User.Id);
    }

    [Fact]
    public async Task Register_ErrorsUseTheAgreedStatusCodes()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await client.RegisterAsync(email);

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FirstName = "A", LastName = "B", Email = email, Password = "secret1", ConfirmPassword = "secret1"
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);

        var invalid = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FirstName = "A", LastName = "B", Email = UniqueEmail(), Password = "123", ConfirmPassword = "456"
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var errors = (await invalid.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Password", out _));
        Assert.True(errors.TryGetProperty("ConfirmPassword", out _));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await client.RegisterAsync(email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = "wrong-password" }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Profile_CanBeReadAndUpdated()
    {
        var client = _factory.CreateClient();
        var account = await client.RegisterAsync();
        client.WithToken(account.AccessToken);

        var profile = await (await client.GetAsync("/api/profile")).ReadAsync<ProfileDto>();
        Assert.Equal(account.User.Email, profile.Email);

        var update = new UpdateProfileRequestDto
        {
            Title = "Ms", FirstName = "Aysun", LastName = "Panahli", DateOfBirth = new DateOnly(2004, 6, 16),
            NationalityCode = "AZ", Email = account.User.Email, Phone = "+994 50 834 39 43"
        };
        var updated = await client.PutAsJsonAsync("/api/profile", update, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("+994508343943", (await updated.ReadAsync<ProfileDto>()).Phone);

        update.Title = "Dr";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/profile", update, Json)).StatusCode);

        var other = await _factory.CreateClient().RegisterAsync();
        update.Title = "Ms";
        update.Email = other.User.Email;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/profile", update, Json)).StatusCode);
    }

    [Fact]
    public async Task SavedPassengers_FullCycle_AndOwnership()
    {
        var client = _factory.CreateClient().WithToken((await _factory.CreateClient().RegisterAsync()).AccessToken);
        var stranger = _factory.CreateClient().WithToken((await _factory.CreateClient().RegisterAsync()).AccessToken);

        var created = await client.PostAsJsonAsync("/api/profile/saved-passengers", new SavedPassengerRequestDto
        {
            FirstName = "Orkhan", LastName = "Aliyev", Relationship = "Spouse", Gender = Domain.Enums.Gender.Male
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.ReadJsonAsync();
        Assert.Equal("Male", json.GetProperty("gender").GetString()); // enums travel as text
        var passenger = json.Deserialize<SavedPassengerDto>(Json)!;

        var list = await (await client.GetAsync("/api/profile/saved-passengers")).ReadAsync<List<SavedPassengerDto>>();
        Assert.Single(list);

        var renamed = await client.PutAsJsonAsync($"/api/profile/saved-passengers/{passenger.Id}",
            new SavedPassengerRequestDto { FirstName = "Orkhan", LastName = "Aliyev-Panahli" }, Json);
        Assert.Equal("Aliyev-Panahli", (await renamed.ReadAsync<SavedPassengerDto>()).LastName);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/profile/saved-passengers/{passenger.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/profile/saved-passengers/{passenger.Id}")).StatusCode);
        Assert.Empty(await (await client.GetAsync("/api/profile/saved-passengers")).ReadAsync<List<SavedPassengerDto>>());
    }
}
