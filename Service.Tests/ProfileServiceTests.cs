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

        Assert.Null(result.Title);
        Assert.Equal("AZ", result.NationalityCode);
        Assert.Equal("aysun@example.com", result.Email);
        Assert.Equal("+994508343943", result.Phone);
    }

    [Fact]
    public async Task UpdateProfile_NewEmail_IsRefused_BecauseOnlyTheCodeFlowOfTheSettingsPageChangesIt()
    {
        var verifiedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        _users.Find(1)!.EmailVerifiedAt = verifiedAt;

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().UpdateProfileAsync(1, ProfileRequest("new.address@example.com")));

        Assert.Equal("email_change_via_settings", refused.Code);
        Assert.Equal("aysun@example.com", _users.Find(1)!.Email);
        Assert.Equal(verifiedAt, _users.Find(1)!.EmailVerifiedAt);
        Assert.Empty(_accountEmail.EmailChangedFor);
    }

    [Fact]
    public async Task UpdateProfile_SameEmail_KeepsTheConfirmation()
    {
        var verifiedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        _users.Find(1)!.EmailVerifiedAt = verifiedAt;

        var kept = await CreateService().UpdateProfileAsync(1, ProfileRequest(" AYSUN@example.com "));

        Assert.Equal(verifiedAt, _users.Find(1)!.EmailVerifiedAt);
        Assert.True(kept.EmailVerified);
        Assert.Empty(_accountEmail.EmailChangedFor);
    }

    [Fact]
    public async Task UpdateProfile_EmailOfAnotherAccount_IsRefused_AndNothingIsChanged()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().UpdateProfileAsync(1, ProfileRequest("other@example.com")));
        Assert.Equal("aysun@example.com", _users.Find(1)!.Email);
    }

    [Fact]
    public async Task UpdateProfile_IgnoresSalutation()
    {
        var request = ProfileRequest();
        request.Title = "Dr";

        Assert.Null((await CreateService().UpdateProfileAsync(1, request)).Title);
    }

    [Fact]
    public async Task UpdateProfile_SavesTheFathersName_AndNormalizesTheDocumentNumbers()
    {
        var request = ProfileRequest();
        request.FatherName = "  Elşən ";
        request.PassportNumber = "aze-1234 5678";
        request.NationalIdNumber = " aa 1234567 ";

        var result = await CreateService().UpdateProfileAsync(1, request);

        Assert.Equal("Elşən", result.FatherName);
        Assert.Equal("AZE12345678", result.PassportNumber);
        Assert.Equal("AA1234567", result.NationalIdNumber);
        Assert.Equal("AZE12345678", _users.Find(1)!.PassportNumber);
    }

    [Fact]
    public async Task UpdateProfile_FieldsThatAreNotSent_AreKept_AndEmptyOnesAreRemoved()
    {
        var first = ProfileRequest();
        first.FatherName = "Elşən";
        first.PassportNumber = "AZE12345678";
        first.NationalIdNumber = "AA1234567";
        await CreateService().UpdateProfileAsync(1, first);

        // An older page does not send them: nothing is lost.
        var kept = await CreateService().UpdateProfileAsync(1, ProfileRequest());
        Assert.Equal("Elşən", kept.FatherName);
        Assert.Equal("AZE12345678", kept.PassportNumber);
        Assert.Equal("AA1234567", kept.NationalIdNumber);

        // Sent empty on purpose: removed.
        var cleared = ProfileRequest();
        cleared.FatherName = "";
        cleared.PassportNumber = " ";
        cleared.NationalIdNumber = "";
        var removed = await CreateService().UpdateProfileAsync(1, cleared);
        Assert.Null(removed.FatherName);
        Assert.Null(removed.PassportNumber);
        Assert.Null(removed.NationalIdNumber);
    }

    [Theory]
    [InlineData("FatherName", "Elşən 123")]
    [InlineData("PassportNumber", "A1")]
    [InlineData("PassportNumber", "AB12#4567")]
    [InlineData("NationalIdNumber", "123456789012345678901")]
    public async Task UpdateProfile_BadIdentityFields_AreRefused(string field, string value)
    {
        var request = ProfileRequest();
        typeof(UpdateProfileRequestDto).GetProperty(field)!.SetValue(request, value);

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().UpdateProfileAsync(1, request));
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
