using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Repository.Data;
using Repository.Repositories;

namespace Repository.Tests;

public class RepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Base_Add_GetById_Update_Delete_Works()
    {
        await using var context = CreateContext();
        var repository = new BaseRepository<MealOption>(context);

        var meal = new MealOption { Code = "classic", Name = "Classic Dining", Price = 18 };
        await repository.AddAsync(meal);
        await repository.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(meal.Id);
        Assert.NotNull(loaded);
        Assert.Null(loaded!.UpdatedAt);

        loaded.Price = 20;
        repository.Update(loaded);
        await repository.SaveChangesAsync();
        Assert.Equal(20, (await repository.GetByIdAsync(meal.Id))!.Price);
        Assert.NotNull(loaded.UpdatedAt);

        repository.Delete(loaded);
        await repository.SaveChangesAsync();
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task Base_Update_AttachesDetachedEntity()
    {
        await using var context = CreateContext();
        var repository = new BaseRepository<BaggageOption>(context);
        await repository.AddAsync(new BaggageOption { Code = "b10", Name = "+10 kg", WeightKg = 10, Price = 30 });
        await repository.SaveChangesAsync();

        var detached = new BaggageOption { Id = 1, Code = "b10", Name = "+10 kg", WeightKg = 10, Price = 35 };
        context.ChangeTracker.Clear();
        repository.Update(detached);
        await repository.SaveChangesAsync();

        context.ChangeTracker.Clear();
        Assert.Equal(35, (await repository.GetByIdAsync(1))!.Price);
    }

    [Fact]
    public async Task Airport_GetByCode_And_GetActive()
    {
        await using var context = CreateContext();
        var repository = new AirportRepository(context);
        await repository.AddAsync(new Airport { Code = "GYD", Name = "Heydar Aliyev", City = "Baku", CountryCode = "AZ" });
        await repository.AddAsync(new Airport { Code = "XXX", Name = "Closed", City = "Nowhere", CountryCode = "AZ", IsActive = false });
        await repository.SaveChangesAsync();

        Assert.NotNull(await repository.GetByCodeAsync("GYD"));
        Assert.Null(await repository.GetByCodeAsync("LHR"));
        var active = await repository.GetActiveAsync();
        Assert.Single(active);
    }

    [Fact]
    public async Task User_EmailExists()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        await repository.AddAsync(new User { Email = "a@b.com", PasswordHash = "x", FirstName = "A", LastName = "B" });
        await repository.SaveChangesAsync();

        Assert.True(await repository.EmailExistsAsync("a@b.com"));
        Assert.False(await repository.EmailExistsAsync("c@d.com"));
        Assert.NotNull(await repository.GetByEmailAsync("a@b.com"));
    }

    [Fact]
    public async Task CancellationRule_ReturnsCabinRulesOrderedByMinDaysDescending()
    {
        await using var context = CreateContext();
        var repository = new CancellationRuleRepository(context);
        await repository.AddAsync(new CancellationRule { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 14, FeePercent = 40 });
        await repository.AddAsync(new CancellationRule { CabinClass = CabinClass.Economy, MinDaysBeforeDeparture = 30, FeePercent = 20 });
        await repository.AddAsync(new CancellationRule { CabinClass = CabinClass.Business, MinDaysBeforeDeparture = 7, FeePercent = 0 });
        await repository.SaveChangesAsync();

        var rules = await repository.GetByCabinClassAsync(CabinClass.Economy);
        Assert.Equal(new[] { 30, 14 }, rules.Select(r => r.MinDaysBeforeDeparture));
    }

    [Fact]
    public async Task Flight_Search_FiltersByRouteDateAndStatus()
    {
        await using var context = CreateContext();
        var origin = new Airport { Code = "GYD", Name = "Baku", City = "Baku", CountryCode = "AZ" };
        var destination = new Airport { Code = "LHR", Name = "Heathrow", City = "London", CountryCode = "GB" };
        var route = new Route { Origin = origin, Destination = destination, DistanceKm = 4000, DurationMinutes = 340 };
        var aircraft = new Aircraft { Model = "787-9", RegistrationNumber = "4K-AZ01" };
        context.AddRange(route, aircraft);
        await context.SaveChangesAsync();

        var day = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        context.Flights.AddRange(
            new Flight { FlightNumber = "AV101", Route = route, Aircraft = aircraft, DepartureAt = day.AddHours(9), ArrivalAt = day.AddHours(15) },
            new Flight { FlightNumber = "AV401", Route = route, Aircraft = aircraft, DepartureAt = day.AddHours(15), ArrivalAt = day.AddHours(21) },
            new Flight { FlightNumber = "AV999", Route = route, Aircraft = aircraft, DepartureAt = day.AddHours(12), ArrivalAt = day.AddHours(18), Status = FlightStatus.Cancelled },
            new Flight { FlightNumber = "AV102", Route = route, Aircraft = aircraft, DepartureAt = day.AddDays(1).AddHours(9), ArrivalAt = day.AddDays(1).AddHours(15) });
        await context.SaveChangesAsync();

        var repository = new FlightRepository(context);
        var result = await repository.SearchAsync(origin.Id, destination.Id, new DateOnly(2026, 11, 10));

        Assert.Equal(new[] { "AV101", "AV401" }, result.Select(f => f.FlightNumber));
    }

    [Fact]
    public async Task Booking_GetByPnr_And_PnrExists()
    {
        await using var context = CreateContext();
        var repository = new BookingRepository(context);
        await repository.AddAsync(new Booking { Pnr = "AV7K92", ContactEmail = "a@b.com", ContactPhone = "+994", TripType = TripType.OneWay });
        await repository.SaveChangesAsync();

        Assert.True(await repository.PnrExistsAsync("AV7K92"));
        Assert.False(await repository.PnrExistsAsync("ZZZZZZ"));
        Assert.NotNull(await repository.GetByPnrAsync("AV7K92"));
    }

    [Fact]
    public async Task Notification_UnreadCount_CountsOnlyUnreadOfUser()
    {
        await using var context = CreateContext();
        var user = new User { Email = "a@b.com", PasswordHash = "x", FirstName = "A", LastName = "B" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new NotificationRepository(context);
        await repository.AddAsync(new Notification { UserId = user.Id, Type = NotificationType.BookingConfirmed, Title = "t", Message = "m" });
        await repository.AddAsync(new Notification { UserId = user.Id, Type = NotificationType.PaymentSuccessful, Title = "t", Message = "m", IsRead = true });
        await repository.SaveChangesAsync();

        Assert.Equal(1, await repository.GetUnreadCountAsync(user.Id));
        Assert.Equal(2, (await repository.GetByUserIdAsync(user.Id)).Count);
    }
}
