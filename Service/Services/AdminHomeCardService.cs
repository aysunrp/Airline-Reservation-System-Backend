using Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Cms;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminHomeCardService : IAdminHomeCardService
{
    private readonly IHomeCardRepository _cards;
    private readonly ISiteMediaStorage _media;
    private readonly IValidator<HomeCardRequestDto> _validator;

    public AdminHomeCardService(IHomeCardRepository cards, ISiteMediaStorage media, IValidator<HomeCardRequestDto> validator)
    {
        _cards = cards;
        _media = media;
        _validator = validator;
    }

    public async Task<IReadOnlyList<HomeCardDto>> GetAllAsync(string? section, CancellationToken cancellationToken = default)
        => (await _cards.GetOrderedAsync(NormalizeSection(section), false, cancellationToken)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<HomeCardDto>> GetActiveAsync(string? section, CancellationToken cancellationToken = default)
        => (await _cards.GetOrderedAsync(NormalizeSection(section), true, cancellationToken)).Select(ToDto).ToList();

    public async Task<HomeCardDto> GetAsync(int id, CancellationToken cancellationToken = default)
        => ToDto(await FindAsync(id, cancellationToken));

    public async Task<HomeCardDto> CreateAsync(HomeCardRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var card = new HomeCard();
        Apply(request, card);
        card.SortOrder = request.SortOrder ?? await _cards.NextSortOrderAsync(card.Section, cancellationToken);
        await _cards.AddAsync(card, cancellationToken);
        await _cards.SaveChangesAsync(cancellationToken);
        return ToDto(card);
    }

    public async Task<HomeCardDto> UpdateAsync(int id, HomeCardRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var card = await FindAsync(id, cancellationToken);
        Apply(request, card);
        if (request.SortOrder is not null)
        {
            card.SortOrder = request.SortOrder.Value;
        }

        _cards.Update(card);
        await _cards.SaveChangesAsync(cancellationToken);
        return ToDto(card);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var card = await FindAsync(id, cancellationToken);
        var image = card.ImagePath;
        _cards.Delete(card);
        await _cards.SaveChangesAsync(cancellationToken);
        await DeleteFileIfUnusedAsync(image, id, cancellationToken);
    }

    public async Task<CardImageDto> UploadImageAsync(int id, IFormFile file, CancellationToken cancellationToken = default)
    {
        var card = await FindAsync(id, cancellationToken);
        var previous = card.ImagePath;

        // The storage checks the extension, the real content and the size, and gives the file a name nobody can guess.
        var relative = await _media.SaveImageAsync(SiteMediaCategories.Home, file, cancellationToken: cancellationToken);
        card.ImagePath = relative;
        _cards.Update(card);
        await _cards.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previous, relative, StringComparison.Ordinal))
        {
            await DeleteFileIfUnusedAsync(previous, id, cancellationToken);
        }

        return new CardImageDto(relative, ImageUrlOf(relative)!);
    }

    public async Task RemoveImageAsync(int id, CancellationToken cancellationToken = default)
    {
        var card = await FindAsync(id, cancellationToken);
        var previous = card.ImagePath;
        card.ImagePath = null;
        _cards.Update(card);
        await _cards.SaveChangesAsync(cancellationToken);
        await DeleteFileIfUnusedAsync(previous, id, cancellationToken);
    }

    /// <summary>Only files this feature uploaded (home/...) are ever deleted, and only when no other card shows them.</summary>
    private async Task DeleteFileIfUnusedAsync(string? path, int exceptCardId, CancellationToken cancellationToken)
    {
        if (!IsUploadedHomeFile(path) || await _cards.ImagePathUsedAsync(path!, exceptCardId, cancellationToken))
        {
            return;
        }

        await _media.TryDeleteRelativeFileAsync(path, cancellationToken);
    }

    private static bool IsUploadedHomeFile(string? path)
        => !string.IsNullOrWhiteSpace(path) && path.Trim().StartsWith(SiteMediaCategories.Home + "/", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeSection(string? section)
        => string.IsNullOrWhiteSpace(section) ? null : section.Trim().ToLowerInvariant();

    private static void Apply(HomeCardRequestDto request, HomeCard card)
    {
        card.Section = request.Section.Trim().ToLowerInvariant();
        card.Title = request.Title.Trim();
        card.Description = request.Description.Trim();
        card.Label = Clean(request.Label);
        card.LinkText = Clean(request.LinkText);
        card.LinkUrl = Clean(request.LinkUrl);
        card.IconKey = Clean(request.IconKey);
        card.ImageAlt = Clean(request.ImageAlt);
        card.IsActive = request.IsActive;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<HomeCard> FindAsync(int id, CancellationToken cancellationToken)
        => await _cards.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Kart tapılmadı.");

    /// <summary>An uploaded file loads from the media address; a path of the site itself (assets/...) is used as it is.</summary>
    private string? ImageUrlOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim();
        return trimmed.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : _media.ToPublicUrl(trimmed);
    }

    private HomeCardDto ToDto(HomeCard card)
        => new(card.Id, card.Section, card.Title, card.Description, card.Label, card.LinkText, card.LinkUrl, card.IconKey,
            card.ImagePath, ImageUrlOf(card.ImagePath), card.ImageAlt, card.SortOrder, card.IsActive);
}
