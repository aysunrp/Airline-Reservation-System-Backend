using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Service.Helpers.DTOs.Destinations;
using Service.Services.Interfaces;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>Deleting catalog countries and cities: only a SuperAdmin, never while a route uses their airports, and the seed must not bring them back.</summary>
public class DestinationDeleteEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DestinationDeleteEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.IsConfigured = false;
    }

    private async Task<List<DestinationCountryDto>> SeededCatalogAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDestinationCatalogSeeder>().SeedAsync();
        return await (await _factory.CreateClient().GetAsync("/api/destinations")).ReadAsync<List<DestinationCountryDto>>();
    }

    [Fact]
    public async Task OnlyASuperAdmin_DeletesACity_AndTheSeedDoesNotBringItBack()
    {
        var catalog = await SeededCatalogAsync();
        var marseille = catalog.Single(c => c.Code == "FR").Cities.Single(c => c.Name == "Marseille");
        var admin = (await _factory.CreateAccountAsync(UserRole.Admin)).Client;
        var super = (await _factory.CreateAccountAsync(UserRole.SuperAdmin)).Client;

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().DeleteAsync($"/api/admin/destinations/cities/{marseille.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.DeleteAsync($"/api/admin/destinations/cities/{marseille.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await super.DeleteAsync("/api/admin/destinations/cities/999999")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await super.DeleteAsync($"/api/admin/destinations/cities/{marseille.Id}")).StatusCode);
        var after = await (await super.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>();
        Assert.DoesNotContain(after.Single(c => c.Code == "FR").Cities, c => c.Name == "Marseille");
        Assert.Contains(after.Single(c => c.Code == "FR").Cities, c => c.Name == "Paris");

        // Starting the API again runs the seed: what an admin deleted stays deleted.
        var again = await SeededCatalogAsync();
        Assert.DoesNotContain(again.Single(c => c.Code == "FR").Cities, c => c.Name == "Marseille");
    }

    [Fact]
    public async Task ACityWhoseAirportHasRoutes_IsRefusedWith409()
    {
        var catalog = await SeededCatalogAsync();
        var super = (await _factory.CreateAccountAsync(UserRole.SuperAdmin)).Client;
        var route = new { originCode = "GYD", destinationCode = "CDG", distanceKm = 3400, durationMinutes = 330, isActive = true };
        var created = await super.PostAsJsonAsync("/api/admin/routes", route, Json);
        Assert.True(created.IsSuccessStatusCode);

        var paris = catalog.Single(c => c.Code == "FR").Cities.Single(c => c.Name == "Paris");
        Assert.Equal(HttpStatusCode.Conflict, (await super.DeleteAsync($"/api/admin/destinations/cities/{paris.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await super.DeleteAsync($"/api/admin/destinations/countries/{catalog.Single(c => c.Code == "FR").Id}")).StatusCode);
    }

    [Fact]
    public async Task ACountryWithoutUsedAirports_IsDeletedWithItsCities()
    {
        var catalog = await SeededCatalogAsync();
        var maldives = catalog.Single(c => c.Code == "MV");
        var super = (await _factory.CreateAccountAsync(UserRole.SuperAdmin)).Client;

        Assert.Equal(HttpStatusCode.NoContent, (await super.DeleteAsync($"/api/admin/destinations/countries/{maldives.Id}")).StatusCode);
        var after = await (await super.GetAsync("/api/admin/destinations")).ReadAsync<List<DestinationCountryDto>>();
        Assert.DoesNotContain(after, c => c.Code == "MV");

        var again = await SeededCatalogAsync();
        Assert.DoesNotContain(again, c => c.Code == "MV");
    }
}
