using System.Net;
using System.Net.Http.Json;
using Service.Helpers.DTOs.Account;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// What the passenger form starts with: <c>GET /api/profile</c> answers for the token's own account only, carries nothing secret,
/// leaves out what the account never gave, and reading it changes nothing.
/// </summary>
public class ProfileAutofillEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProfileAutofillEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, AuthResponseDto Account)> AccountAsync(string name)
    {
        var account = await _factory.CreateClient().RegisterAsync(UniqueEmail(name));
        return (_factory.ClientWith(account.AccessToken), account);
    }

    [Fact]
    public async Task EachAccount_GetsItsOwnDetails_AndNoOneElses()
    {
        var (aysun, aysunAccount) = await AccountAsync("aysun");
        var (orkhan, orkhanAccount) = await AccountAsync("orkhan");
        var update = new UpdateProfileRequestDto
        {
            Title = "Ms", FirstName = "Aysun", LastName = "Panahli", DateOfBirth = new DateOnly(1995, 4, 12),
            NationalityCode = "AZ", Email = aysunAccount.User.Email, Phone = "+994501234567"
        };
        Assert.Equal(HttpStatusCode.OK, (await aysun.PutAsJsonAsync("/api/profile", update, Json)).StatusCode);

        var first = await (await aysun.GetAsync("/api/profile")).ReadAsync<ProfileDto>();
        var second = await (await orkhan.GetAsync("/api/profile")).ReadAsync<ProfileDto>();

        Assert.Equal((aysunAccount.User.Id, "Ms", "Panahli", new DateOnly(1995, 4, 12), "AZ", "+994501234567"),
            (first.Id, first.Title, first.LastName, first.DateOfBirth!.Value, first.NationalityCode, first.Phone));
        Assert.Equal(orkhanAccount.User.Id, second.Id);
        Assert.Equal(orkhanAccount.User.Email, second.Email);
        Assert.NotEqual(first.Email, second.Email);
    }

    [Fact]
    public async Task WhatTheAccountNeverGave_StaysEmpty_NothingIsMadeUp()
    {
        var (client, _) = await AccountAsync("fresh");

        var profile = await (await client.GetAsync("/api/profile")).ReadAsync<ProfileDto>();

        Assert.Null(profile.Title);
        Assert.Null(profile.DateOfBirth);
        Assert.Null(profile.NationalityCode);
        Assert.Null(profile.Phone);
    }

    [Fact]
    public async Task TheAnswer_CarriesNothingSecret()
    {
        var (client, _) = await AccountAsync("plain");

        var json = await (await client.GetAsync("/api/profile")).Content.ReadAsStringAsync();

        foreach (var word in new[] { "password", "hash", "token", "secret", "role" })
        {
            Assert.DoesNotContain(word, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task WithoutSignIn_OrWithAnotherId_NobodysDetailsComeBack()
    {
        var (_, victim) = await AccountAsync("victim");
        var (attacker, _) = await AccountAsync("attacker");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/profile")).StatusCode);

        // There is no route that takes an id: the account always comes from the token.
        var byId = await attacker.GetAsync($"/api/profile/{victim.User.Id}");
        Assert.True(byId.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, byId.StatusCode.ToString());
        var withQuery = await (await attacker.GetAsync($"/api/profile?id={victim.User.Id}&userId={victim.User.Id}")).ReadAsync<ProfileDto>();
        Assert.NotEqual(victim.User.Id, withQuery.Id);
    }

    [Fact]
    public async Task ReadingTheProfile_ChangesNothing()
    {
        var (client, _) = await AccountAsync("reader");
        var before = await (await client.GetAsync("/api/profile")).ReadAsync<ProfileDto>();

        for (var i = 0; i < 3; i++)
        {
            await client.GetAsync("/api/profile");
        }

        Assert.Equal(before, await (await client.GetAsync("/api/profile")).ReadAsync<ProfileDto>());
    }
}
