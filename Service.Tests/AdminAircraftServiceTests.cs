using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Aircraft;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

public class AdminAircraftServiceTests
{
    private readonly FakeAircraftRepository _repository = new();

    private AdminAircraftService CreateService()
        => new(_repository, new AircraftRequestValidator(), Options.Create(new SeatLayoutSettings()));

    private static AircraftRequestDto Dreamliner(string registration = "4k-av01") => new()
    {
        Name = "AEROVA Dreamliner 1",
        Model = "787-9",
        RegistrationNumber = registration,
        Status = AircraftStatus.Active,
        BusinessSeats = 28,
        ComfortSeats = 42,
        EconomySeats = 216
    };

    [Fact]
    public async Task Create_GeneratesSeatsFromCabinCounts()
    {
        var result = await CreateService().CreateAsync(Dreamliner());

        Assert.Equal("4K-AV01", result.Aircraft.RegistrationNumber);
        Assert.Equal((28, 42, 216, 286), (result.Aircraft.BusinessSeats, result.Aircraft.ComfortSeats, result.Aircraft.EconomySeats, result.Aircraft.TotalSeats));

        string[] Labels(CabinClass cabin) => result.Seats.Where(s => s.CabinClass == cabin).Select(s => s.Label).ToArray();

        // Business 2+2 in rows 1-7, gap row 8, Comfort 2+1+2 in rows 9-17 (last row partly filled), gap row 18, Economy 3+3 from row 19.
        Assert.Equal(new[] { "1A", "1B", "1C", "1D" }, Labels(CabinClass.Business).Take(4));
        Assert.Equal("7D", Labels(CabinClass.Business).Last());
        Assert.Equal(new[] { "9A", "9B", "9C", "9D", "9E" }, Labels(CabinClass.Comfort).Take(5));
        Assert.Equal(new[] { "17A", "17B" }, Labels(CabinClass.Comfort).TakeLast(2));
        Assert.Equal("19A", Labels(CabinClass.Economy).First());
        Assert.Equal("54F", Labels(CabinClass.Economy).Last());
        Assert.Equal(result.Seats.Count, result.Seats.Select(s => s.Label).Distinct().Count());
    }

    [Fact]
    public async Task EconomyOnlyAircraft_StartsAtRowOne()
    {
        var request = Dreamliner();
        request.BusinessSeats = 0;
        request.ComfortSeats = 0;
        request.EconomySeats = 7;

        var result = await CreateService().CreateAsync(request);

        Assert.Equal(new[] { "1A", "1B", "1C", "1D", "1E", "1F", "2A" }, result.Seats.Select(s => s.Label));
    }

    [Fact]
    public async Task DuplicateRegistration_ThrowsConflict()
    {
        var service = CreateService();
        await service.CreateAsync(Dreamliner("4K-AV01"));

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Dreamliner(" 4k-av01 ")));
    }

    [Fact]
    public async Task Update_ChangesLayoutOnlyWhileUnused()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Dreamliner());
        var request = Dreamliner();
        request.EconomySeats = 150;

        var updated = await service.UpdateAsync(created.Aircraft.Id, request);
        Assert.Equal(150, updated.Aircraft.EconomySeats);

        _repository.InUse.Add(created.Aircraft.Id);
        request.EconomySeats = 120;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(created.Aircraft.Id, request));

        // Other fields can still change while in use.
        request.EconomySeats = 150;
        request.Status = AircraftStatus.Maintenance;
        Assert.Equal(AircraftStatus.Maintenance, (await service.UpdateAsync(created.Aircraft.Id, request)).Aircraft.Status);
    }

    [Fact]
    public async Task Delete_IsBlockedWhileInUse()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Dreamliner());
        _repository.InUse.Add(created.Aircraft.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(created.Aircraft.Id));

        _repository.InUse.Clear();
        await service.DeleteAsync(created.Aircraft.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailsAsync(created.Aircraft.Id));
    }

    [Fact]
    public async Task Search_FiltersByNameModelOrRegistration()
    {
        var service = CreateService();
        await service.CreateAsync(Dreamliner("4K-AV01"));
        var a350 = Dreamliner("4K-AV08");
        a350.Name = "AEROVA Horizon";
        a350.Model = "A350-900";
        await service.CreateAsync(a350);

        Assert.Single(await service.GetAllAsync("a350"));
        Assert.Single(await service.GetAllAsync("AV01"));
        Assert.Equal(2, (await service.GetAllAsync()).Count);
    }

    [Fact]
    public async Task NoSeats_ThrowsValidation()
    {
        var request = Dreamliner();
        request.BusinessSeats = request.ComfortSeats = request.EconomySeats = 0;

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().CreateAsync(request));
    }
}
