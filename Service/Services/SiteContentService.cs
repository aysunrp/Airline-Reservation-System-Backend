using Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.SiteContent;
using Service.Helpers.Exceptions;
using Service.Helpers.Seeding;
using Service.Services.Interfaces;

namespace Service.Services;

public class SiteContentService : ISiteContentService
{
    private readonly ISiteContentRepository _contents;
    private readonly IValidator<SiteContentRequestDto> _validator;
    private readonly IValidator<SiteContentCreateRequestDto> _createValidator;

    public SiteContentService(
        ISiteContentRepository contents,
        IValidator<SiteContentRequestDto> validator,
        IValidator<SiteContentCreateRequestDto> createValidator)
    {
        _contents = contents;
        _validator = validator;
        _createValidator = createValidator;
    }

    public async Task<IReadOnlyList<SiteContentDto>> GetPageAsync(string page, CancellationToken cancellationToken = default)
    {
        var items = await _contents.GetByPageAsync(page.Trim().ToLowerInvariant(), cancellationToken);
        if (items.Count == 0)
        {
            throw new NotFoundException("Bu səhifənin mətnləri tapılmadı.");
        }

        return items.Select(ToDto).ToList();
    }

    public async Task<SiteContentDto> UpdateAsync(int id, SiteContentRequestDto? request, CancellationToken cancellationToken = default)
    {
        request ??= new SiteContentRequestDto();
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var content = await _contents.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Mətn tapılmadı.");
        content.TextAz = request.TextAz!.Trim();
        _contents.Update(content);
        await _contents.SaveChangesAsync(cancellationToken);
        return ToDto(content);
    }

    public async Task<SiteContentDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var content = await _contents.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Mətn tapılmadı.");
        return ToDto(content);
    }

    public async Task<SiteContentDto> CreateAsync(SiteContentCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        request ??= new SiteContentCreateRequestDto();
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var page = request.Page!.Trim().ToLowerInvariant();
        var key = request.Key!.Trim();
        if (await _contents.FindTrackedAsync(page, key, cancellationToken) is not null)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(SiteContentCreateRequestDto.Key), "Bu açar artıq mövcuddur.")
            });
        }

        var entity = new SiteContent { Page = page, Key = key, TextAz = request.TextAz!.Trim() };
        await _contents.AddAsync(entity, cancellationToken);
        await _contents.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var content = await _contents.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Mətn tapılmadı.");
        if (SiteContentData.IsSeedKey(content.Page, content.Key))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(SiteContent.Key), "Default mətn silinə bilməz.")
            });
        }

        _contents.Delete(content);
        await _contents.SaveChangesAsync(cancellationToken);
    }

    private static SiteContentDto ToDto(SiteContent c)
        => new(c.Id, c.Page, c.Key, c.TextAz, !SiteContentData.IsSeedKey(c.Page, c.Key));
}
