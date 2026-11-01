using Domain.Entities;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.SiteContent;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Services;

public class SiteContentService : ISiteContentService
{
    private readonly ISiteContentRepository _contents;
    private readonly IValidator<SiteContentRequestDto> _validator;

    public SiteContentService(ISiteContentRepository contents, IValidator<SiteContentRequestDto> validator)
    {
        _contents = contents;
        _validator = validator;
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

    private static SiteContentDto ToDto(SiteContent c) => new(c.Id, c.Page, c.Key, c.TextAz);
}
