using Domain.Entities;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

internal sealed class FakeDestinationCatalogForMedia : FakeRepository<Country>, IDestinationCatalogRepository
{
    public bool DestinationImageInUse;

    public Task<bool> IsImageFileUsedElsewhereAsync(string imageFile, int? exceptCountryId, int? exceptCityId, CancellationToken cancellationToken = default)
        => Task.FromResult(DestinationImageInUse);

    public Task<IReadOnlyList<Country>> GetCatalogAsync(CancellationToken cancellationToken = default) => NotImplementedAsync<IReadOnlyList<Country>>();

    public Task<Country?> GetCountryAsync(int id, CancellationToken cancellationToken = default) => NotImplementedAsync<Country?>();

    public Task<DestinationCity?> GetCityAsync(int id, CancellationToken cancellationToken = default) => NotImplementedAsync<DestinationCity?>();

    public Task<IReadOnlyList<Airport>> GetAirportsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default) => NotImplementedAsync<IReadOnlyList<Airport>>();

    public Task AddCityAsync(DestinationCity city, CancellationToken cancellationToken = default) => NotImplementedAsync<object?>();

    public Task AddAirportAsync(Airport airport, CancellationToken cancellationToken = default) => NotImplementedAsync<object?>();

    public Task<IReadOnlySet<int>> GetAirportIdsWithActiveRoutesAsync(CancellationToken cancellationToken = default) => NotImplementedAsync<IReadOnlySet<int>>();

    public Task<(int Routes, int Schedules)> DeactivateRoutesOfAirportsAsync(IReadOnlyCollection<int> airportIds, CancellationToken cancellationToken = default) => NotImplementedAsync<(int, int)>();

    public Task<(int Routes, int Schedules)> DeactivateRoutesBetweenAsync(IReadOnlyCollection<(string Origin, string Destination)> pairs, CancellationToken cancellationToken = default) => NotImplementedAsync<(int, int)>();

    public Task<IReadOnlyList<Flight>> GetUpcomingFlightsAsync(int originAirportId, IReadOnlyCollection<int> destinationAirportIds, DateTime from, int take, CancellationToken cancellationToken = default) => NotImplementedAsync<IReadOnlyList<Flight>>();


    public Task<DestinationCity?> GetCityWithAirportsAsync(int id, CancellationToken cancellationToken = default) => NotImplementedAsync<DestinationCity?>();

    public Task<Country?> GetCountryWithCitiesAsync(int id, CancellationToken cancellationToken = default) => NotImplementedAsync<Country?>();

    public Task<bool> AnyAirportInUseAsync(IReadOnlyCollection<int> airportIds, CancellationToken cancellationToken = default) => NotImplementedAsync<bool>();

    public void RemoveCity(DestinationCity city) => throw new NotImplementedException();

    public void RemoveCountry(Country country) => throw new NotImplementedException();

    public Task<HashSet<string>> GetRemovedKeysAsync(CancellationToken cancellationToken = default) => NotImplementedAsync<HashSet<string>>();

    public Task AddRemovedKeyAsync(string key, CancellationToken cancellationToken = default) => NotImplementedAsync<object?>();

    private static Task<T> NotImplementedAsync<T>() => Task.FromException<T>(new NotImplementedException());
}

public class SiteMediaReferenceServiceTests
{
    private sealed class TestMediaStorage : ISiteMediaStorage
    {
        public int DeleteCalls;

        public Task<bool> TryDeleteRelativeFileAsync(string? relativePath, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return Task.FromResult(true);
        }

        public string ToRelativePath(string publicOrRelativePath) => publicOrRelativePath.TrimStart('/').Replace("media/", "");

        public string ToPublicUrl(string relativePath) => "/media/" + relativePath;

        public Task<string> SaveImageAsync(string category, Microsoft.AspNetCore.Http.IFormFile file, long? maxBytes = null, CancellationToken cancellationToken = default) => NotImplementedTask<string>();

        public Task<string> SaveHeroVideoAsync(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken cancellationToken = default) => NotImplementedTask<string>();

        public Task<string> SaveHeroPosterAsync(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken cancellationToken = default) => NotImplementedTask<string>();

        public string NormalizeHeroMediaUrl(string? url, bool required, bool allowVideoExtensions) => url ?? string.Empty;

        private static Task<T> NotImplementedTask<T>() => Task.FromException<T>(new NotImplementedException());
    }

    private static SiteMediaReferenceService Create(FakeSiteContentRepository contents, FakeDestinationCatalogForMedia catalog, TestMediaStorage media)
        => new(catalog, media);

    [Fact]
    public async Task DeleteIsSkipped_WhenDestinationCatalogStillReferencesTheSamePath()
    {
        const string path = "destinations/shared.webp";
        var contents = new FakeSiteContentRepository();
        var catalog = new FakeDestinationCatalogForMedia { DestinationImageInUse = true };
        var media = new TestMediaStorage();
        var service = Create(contents, catalog, media);

        var deleted = await service.TryDeleteFileIfUnreferencedAsync(path);

        Assert.False(deleted);
        Assert.Equal(0, media.DeleteCalls);
    }

    [Fact]
    public async Task DeleteProceeds_WhenNoCrossCategoryReferencesRemain()
    {
        const string path = "destinations/orphan.webp";
        var contents = new FakeSiteContentRepository();
        var catalog = new FakeDestinationCatalogForMedia { DestinationImageInUse = false };
        var media = new TestMediaStorage();
        var service = Create(contents, catalog, media);

        var deleted = await service.TryDeleteFileIfUnreferencedAsync(path, new SiteMediaReferenceExclusions(ExceptCityId: 9));

        Assert.True(deleted);
        Assert.Equal(1, media.DeleteCalls);
    }
}
