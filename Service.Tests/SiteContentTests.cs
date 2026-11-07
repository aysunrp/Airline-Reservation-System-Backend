using Domain.Entities;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.SiteContent;
using Service.Helpers.Exceptions;
using Service.Helpers.Seeding;
using Service.Helpers.Validators;
using Service.Services;

namespace Service.Tests;

/// <summary>A page-text store in memory that also counts how many times the changes were saved.</summary>
internal sealed class FakeSiteContentRepository : FakeRepository<SiteContent>, ISiteContentRepository
{
    public FakeSiteContentRepository(params SiteContent[] items) : base(items)
    {
    }

    public int Saves { get; private set; }

    public IReadOnlyList<SiteContent> All => Items;

    public Task<IReadOnlyList<SiteContent>> GetByPageAsync(string page, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SiteContent>>(Items.Where(c => c.Page == page).OrderBy(c => c.Key, StringComparer.Ordinal).ToList());

    public Task<HashSet<string>> GetKeysAsync(string page, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Where(c => c.Page == page).Select(c => c.Key).ToHashSet());

    public Task<SiteContent?> FindTrackedAsync(string page, string key, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(c => c.Page == page && c.Key == key));

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.FromResult(1);
    }
}

public class SiteContentRequestValidatorTests
{
    private readonly SiteContentRequestValidator _validator = new();

    private string[] Errors(string? text) => _validator.Validate(new SiteContentRequestDto { TextAz = text }).Errors.Select(e => e.ErrorMessage).ToArray();

    [Theory]
    [InlineData("Bir səyahətdən daha artığı.")]
    [InlineData("a")]
    public void ANormalText_IsAccepted(string text) => Assert.Empty(Errors(text));

    [Fact]
    public void ATextOfExactly1000Characters_IsAccepted() => Assert.Empty(Errors(new string('ə', 1000)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void AMissingOrBlankText_IsRefused_InAzerbaijani(string? text)
        => Assert.Equal("Mətn boş ola bilməz.", Assert.Single(Errors(text)));

    [Fact]
    public void ATextOf1001Characters_IsRefused_InAzerbaijani_AndSaysHowLongItIs()
    {
        var message = Assert.Single(Errors(new string('x', 1001)));

        Assert.Equal("Mətn 1000 simvoldan uzun ola bilməz. İndi 1001 simvol var.", message);
    }

    [Fact]
    public void TheMessages_AreNotEnglish()
    {
        var all = Errors(null).Concat(Errors(new string('x', 2000)));

        Assert.All(all, message => Assert.DoesNotContain("must", message, StringComparison.OrdinalIgnoreCase));
        Assert.All(all, message => Assert.DoesNotContain("Text Az", message));
    }

    [Fact]
    public void TheValidatorChecksOnlyTheAzerbaijaniText()
        => Assert.Equal(new[] { nameof(SiteContentRequestDto.TextAz) }, typeof(SiteContentRequestDto).GetProperties().Select(p => p.Name));
}

public class SiteContentServiceTests
{
    private static SiteContent Row(int id, string key, string text, string page = "about") => new() { Id = id, Page = page, Key = key, TextAz = text };

    private static SiteContentService Create(FakeSiteContentRepository repository)
        => new(repository, new SiteContentRequestValidator(), new SiteContentCreateRequestValidator());

    [Fact]
    public async Task APage_ListsItsTexts_ByKey_WithTheirIds()
    {
        var repository = new FakeSiteContentRepository(Row(2, "storyTitle", "Hekayə"), Row(1, "heroTitle", "Başlıq"), Row(3, "other", "Başqa", page: "contact"));

        var items = await Create(repository).GetPageAsync("about");

        Assert.Equal(new[] { "heroTitle", "storyTitle" }, items.Select(i => i.Key));
        Assert.Equal(new[] { 1, 2 }, items.Select(i => i.Id));
        Assert.All(items, i => Assert.Equal("about", i.Page));
        Assert.Equal("Başlıq", items[0].TextAz);
    }

    [Theory]
    [InlineData("ABOUT")]
    [InlineData("  about  ")]
    public async Task ThePageName_IsNotCaseSensitive_AndIsTrimmed(string page)
    {
        var items = await Create(new FakeSiteContentRepository(Row(1, "heroTitle", "Başlıq"))).GetPageAsync(page);

        Assert.Single(items);
    }

    [Fact]
    public async Task APageWithoutTexts_IsNotFound_InAzerbaijani()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => Create(new FakeSiteContentRepository(Row(1, "heroTitle", "Başlıq"))).GetPageAsync("nothing"));

        Assert.Equal("Bu səhifənin mətnləri tapılmadı.", exception.Message);
    }

    [Fact]
    public async Task AnEdit_ChangesOnlyTheText_TrimsIt_AndSavesOnce()
    {
        var repository = new FakeSiteContentRepository(Row(1, "heroTitle", "Köhnə"), Row(2, "storyTitle", "Toxunulmaz"));

        var saved = await Create(repository).UpdateAsync(1, new SiteContentRequestDto { TextAz = "  Yeni mətn  " });

        Assert.Equal(new SiteContentDto(1, "about", "heroTitle", "Yeni mətn", CanDelete: false), saved);
        Assert.Equal("Yeni mətn", repository.All.Single(c => c.Id == 1).TextAz);
        Assert.Equal("Toxunulmaz", repository.All.Single(c => c.Id == 2).TextAz);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task AnEdit_KeepsThePageAndTheKey()
    {
        var repository = new FakeSiteContentRepository(Row(1, "heroTitle", "Köhnə"));

        await Create(repository).UpdateAsync(1, new SiteContentRequestDto { TextAz = "Yeni" });

        var row = repository.All.Single();
        Assert.Equal(("about", "heroTitle"), (row.Page, row.Key));
    }

    [Fact]
    public async Task ATextOfExactly1000Characters_IsSaved()
    {
        var repository = new FakeSiteContentRepository(Row(1, "heroTitle", "Köhnə"));

        var saved = await Create(repository).UpdateAsync(1, new SiteContentRequestDto { TextAz = new string('ə', 1000) });

        Assert.Equal(1000, saved.TextAz.Length);
    }

    [Theory]
    [InlineData(null, "Mətn boş ola bilməz.")]
    [InlineData("", "Mətn boş ola bilməz.")]
    [InlineData("    ", "Mətn boş ola bilməz.")]
    public async Task ABlankText_IsRefused_InAzerbaijani_AndNothingIsSaved(string? text, string expected)
    {
        var repository = new FakeSiteContentRepository(Row(1, "heroTitle", "Köhnə"));

        var exception = await Assert.ThrowsAsync<ValidationException>(() => Create(repository).UpdateAsync(1, new SiteContentRequestDto { TextAz = text }));

        Assert.Equal(expected, Assert.Single(exception.Errors).ErrorMessage);
        Assert.Equal("Köhnə", repository.All.Single().TextAz);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task ATooLongText_IsRefused_InAzerbaijani_AndNothingIsSaved()
    {
        var repository = new FakeSiteContentRepository(Row(1, "heroTitle", "Köhnə"));

        var exception = await Assert.ThrowsAsync<ValidationException>(() => Create(repository).UpdateAsync(1, new SiteContentRequestDto { TextAz = new string('x', 1001) }));

        Assert.Equal("Mətn 1000 simvoldan uzun ola bilməz. İndi 1001 simvol var.", Assert.Single(exception.Errors).ErrorMessage);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task AMissingRequestBody_IsAValidationError_NotACrash()
    {
        var repository = new FakeSiteContentRepository(Row(1, "heroTitle", "Köhnə"));

        var exception = await Assert.ThrowsAsync<ValidationException>(() => Create(repository).UpdateAsync(1, null));

        Assert.Equal("Mətn boş ola bilməz.", Assert.Single(exception.Errors).ErrorMessage);
    }

    [Fact]
    public async Task AnUnknownId_IsNotFound_InAzerbaijani()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => Create(new FakeSiteContentRepository(Row(1, "heroTitle", "Köhnə"))).UpdateAsync(99, new SiteContentRequestDto { TextAz = "Yeni" }));

        Assert.Equal("Mətn tapılmadı.", exception.Message);
    }

    [Fact]
    public async Task ATextWithMarkup_IsKeptAsPlainText_NotChanged()
    {
        var repository = new FakeSiteContentRepository(Row(1, "heroTitle", "Köhnə"));
        const string markup = "<b>x</b><script>alert(1)</script>";

        var saved = await Create(repository).UpdateAsync(1, new SiteContentRequestDto { TextAz = markup });

        // The page writes it with textContent and the admin table escapes it, so it is only ever shown as text.
        Assert.Equal(markup, saved.TextAz);
    }

    [Fact]
    public void TheDto_HasNoEnglishOrRussianText()
    {
        var names = typeof(SiteContentDto).GetProperties().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "CanDelete", "Id", "Key", "Page", "TextAz" }, names.OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(typeof(SiteContent).GetProperties(), p => p.Name is "TextEn" or "TextRu");
    }

    [Fact]
    public async Task ACustomKey_CanBeCreated_AndDeleted()
    {
        var repository = new FakeSiteContentRepository(Row(1, "heroTitle", "Seed", "about"));
        var service = Create(repository);

        var created = await service.CreateAsync(new SiteContentCreateRequestDto { Page = "about", Key = "custom.promo", TextAz = "Xüsusi" });

        Assert.True(created.CanDelete);
        Assert.Equal("custom.promo", created.Key);
        Assert.Equal(2, repository.All.Count);

        await service.DeleteAsync(created.Id);
        Assert.Single(repository.All);
    }

    [Fact]
    public async Task ASeedKey_CannotBeDeleted()
    {
        var repository = new FakeSiteContentRepository(Row(1, "heroTitle", "Seed", "about"));

        var exception = await Assert.ThrowsAsync<ValidationException>(() => Create(repository).DeleteAsync(1));

        Assert.Contains("Default mətn silinə bilməz", Assert.Single(exception.Errors).ErrorMessage);
    }
}

public class SiteContentSeederTests
{
    private static SiteContentSeeder Create(FakeSiteContentRepository repository) => new(repository);

    [Fact]
    public async Task AnEmptyStore_GetsEverySeededPageText()
    {
        var repository = new FakeSiteContentRepository();

        var result = await Create(repository).SeedAsync();

        Assert.Equal(new SiteContentSeedResultDto(SiteContentData.TotalSeedCount, 0), result);
        Assert.Equal(SiteContentData.TotalSeedCount, repository.All.Count);
        Assert.Equal(SiteContentData.About.Count, repository.All.Count(c => c.Page == SiteContentData.AboutPage));
        Assert.Equal(SiteContentData.Home.Count, repository.All.Count(c => c.Page == SiteContentData.HomePage));
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task RunningItAgain_AddsNothing_AndSavesNothing()
    {
        var repository = new FakeSiteContentRepository();
        var seeder = Create(repository);
        await seeder.SeedAsync();

        var again = await seeder.SeedAsync();

        Assert.Equal(new SiteContentSeedResultDto(0, SiteContentData.TotalSeedCount), again);
        Assert.Equal(SiteContentData.TotalSeedCount, repository.All.Count);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task AnAdminsEdit_IsNeverOverwritten()
    {
        var repository = new FakeSiteContentRepository(new SiteContent { Id = 1, Page = "about", Key = "heroTitle", TextAz = "Admin redaktəsi" });

        var result = await Create(repository).SeedAsync();

        Assert.Equal("Admin redaktəsi", repository.All.Single(c => c.Key == "heroTitle").TextAz);
        Assert.Equal(SiteContentData.TotalSeedCount - 1, result.Added);
        Assert.Equal(1, result.AlreadyPresent);
    }

    [Fact]
    public async Task OnlyTheMissingTexts_AreAdded()
    {
        var present = SiteContentData.About.Take(10).Select((item, index) => new SiteContent { Id = index + 1, Page = "about", Key = item.Key, TextAz = "Dəyişdirilib" }).ToArray();
        var repository = new FakeSiteContentRepository(present);

        var result = await Create(repository).SeedAsync();

        Assert.Equal(SiteContentData.TotalSeedCount - 10, result.Added);
        Assert.Equal(10, result.AlreadyPresent);
        Assert.All(repository.All.Take(10), c => Assert.Equal("Dəyişdirilib", c.TextAz));
    }

    [Fact]
    public async Task TheTextsOfAnotherPage_AreNotTouchedOrCounted()
    {
        var repository = new FakeSiteContentRepository(new SiteContent { Id = 1, Page = "contact", Key = "heroTitle", TextAz = "Başqa səhifə" });

        var result = await Create(repository).SeedAsync();

        Assert.Equal(SiteContentData.TotalSeedCount, result.Added);
        Assert.Equal("Başqa səhifə", repository.All.Single(c => c.Page == "contact").TextAz);
    }

    [Fact]
    public void TheSeedData_IsComplete_AndFitsTheColumn()
    {
        var items = SiteContentData.About;

        Assert.Equal(78, items.Count);
        Assert.Equal(items.Count, items.Select(i => i.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.TextAz)));
        Assert.All(items, i => Assert.InRange(i.TextAz.Length, 1, 1000));
        Assert.All(items, i => Assert.InRange(i.Key.Length, 1, 100));
        Assert.Equal(new[] { "Key", "TextAz" }, typeof(SiteContentData.Item).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public void TheSeedData_KeepsTheAzerbaijaniTextsOfThePage()
    {
        var byKey = SiteContentData.About.ToDictionary(i => i.Key, i => i.TextAz);

        Assert.Equal("Bir səyahətdən daha artığı.", byKey["heroTitle"]);
        Assert.Equal("AEROVA-nın hekayəsi", byKey["storyTitle"]);
        Assert.Equal("Bronunuzu «Səyahətlərim» bölməsində idarə edə bilərsiniz.", byKey["economyDetail5"]);
        Assert.Equal("AEROVA ilə dünyanın seçilmiş istiqamətlərini kəşf edin.", byKey["finalText"]);
    }
}
