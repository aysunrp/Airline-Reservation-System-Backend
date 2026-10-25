using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Passenger;
using Service.Helpers.Exceptions;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

public class ProfileServiceTests
{
    private static readonly FixedTimeProvider Clock = new(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));

    private readonly FakeUserRepository _users = new(
        new User { Id = 1, Email = "aysun@example.com", FirstName = "Aysun", LastName = "Panahli" },
        new User { Id = 2, Email = "other@example.com", FirstName = "Other", LastName = "User" });

    private readonly FakeSavedPassengerRepository _passengers = new(
        new SavedPassenger { Id = 10, UserId = 1, FirstName = "Orkhan", LastName = "Aliyev", Relationship = "Spouse" },
        new SavedPassenger { Id = 20, UserId = 2, FirstName = "Someone", LastName = "Else" });

    private readonly FakeAccountEmail _accountEmail = new();

    private ProfileService CreateService()
        => new(_users, _passengers, _accountEmail, new UpdateProfileRequestValidator(Clock), new SavedPassengerRequestValidator(Clock));

    private static UpdateProfileRequestDto ProfileRequest(string email = "aysun@example.com") => new()
    {
        Title = "Ms",
        FirstName = "Aysun",
        LastName = "Panahli",
        DateOfBirth = new DateOnly(2004, 6, 16),
        NationalityCode = "AZ",
        Email = email,
        Phone = "+994 50 834 39 43"
    };

    [Fact]
    public async Task UpdateProfile_SavesFieldsAndNormalizesPhone()
    {
        var result = await CreateService().UpdateProfileAsync(1, ProfileRequest(" AYSUN@example.com "));

        Assert.Equal("Ms", result.Title);
        Assert.Equal("AZ", result.NationalityCode);
        Assert.Equal("aysun@example.com", result.Email);
        Assert.Equal("+994508343943", result.Phone);
    }

    [Fact]
    public async Task UpdateProfile_NewEmail_MustBeConfirmedAgain()
    {
        _users.Find(1)!.EmailVerifiedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        await CreateService().UpdateProfileAsync(1, ProfileRequest("new.address@example.com"));

        Assert.Null(_users.Find(1)!.EmailVerifiedAt);
        Assert.Equal(new[] { 1 }, _accountEmail.EmailChangedFor);
    }

    [Fact]
    public async Task UpdateProfile_SameEmail_KeepsTheConfirmation()
    {
        var verifiedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        _users.Find(1)!.EmailVerifiedAt = verifiedAt;

        await CreateService().UpdateProfileAsync(1, ProfileRequest(" AYSUN@example.com "));

        Assert.Equal(verifiedAt, _users.Find(1)!.EmailVerifiedAt);
        Assert.Empty(_accountEmail.EmailChangedFor);
    }

    [Fact]
    public async Task UpdateProfile_EmailOfAnotherAccount_ThrowsConflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => CreateService().UpdateProfileAsync(1, ProfileRequest("other@example.com")));
    }

    [Fact]
    public async Task UpdateProfile_InvalidTitle_ThrowsValidation()
    {
        var request = ProfileRequest();
        request.Title = "Dr";

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().UpdateProfileAsync(1, request));
    }

    [Fact]
    public async Task UpdateProfile_AcceptsMx()
    {
        var request = ProfileRequest();
        request.Title = "Mx";

        Assert.Equal("Mx", (await CreateService().UpdateProfileAsync(1, request)).Title);
    }

    [Fact]
    public async Task UnknownUser_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetProfileAsync(99));
    }

    [Fact]
    public async Task SavedPassengers_OnlyOwnAreListed()
    {
        var list = await CreateService().GetSavedPassengersAsync(1);

        var passenger = Assert.Single(list);
        Assert.Equal("Orkhan", passenger.FirstName);
    }

    [Fact]
    public async Task SavedPassenger_AddUpdateRemove()
    {
        var service = CreateService();

        var added = await service.AddSavedPassengerAsync(1, new SavedPassengerRequestDto
        {
            FirstName = " Nigar ",
            LastName = "Huseynova",
            Relationship = "Child",
            DocumentType = DocumentType.NationalIdCard,
            DocumentNumber = "aa9988776"
        });
        Assert.Equal("Nigar", added.FirstName);
        Assert.Equal("AA9988776", added.DocumentNumber);

        var updated = await service.UpdateSavedPassengerAsync(1, added.Id, new SavedPassengerRequestDto { FirstName = "Nigar", LastName = "Aliyeva" });
        Assert.Equal("Aliyeva", updated.LastName);
        Assert.Null(updated.DocumentNumber);

        await service.RemoveSavedPassengerAsync(1, added.Id);
        Assert.Single(await service.GetSavedPassengersAsync(1));
    }

    [Fact]
    public async Task AnotherUsersPassenger_IsNotFound()
    {
        var service = CreateService();
        var request = new SavedPassengerRequestDto { FirstName = "X", LastName = "Y" };

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateSavedPassengerAsync(1, 20, request));
        await Assert.ThrowsAsync<NotFoundException>(() => service.RemoveSavedPassengerAsync(1, 20));
    }

    [Fact]
    public async Task SavedPassenger_DocumentNumberWithoutType_ThrowsValidation()
    {
        var request = new SavedPassengerRequestDto { FirstName = "A", LastName = "B", DocumentNumber = "C01234567" };

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().AddSavedPassengerAsync(1, request));
    }
}
