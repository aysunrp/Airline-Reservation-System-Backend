using Domain.Entities;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Cms;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminFaqService : IAdminFaqService
{
    private readonly IFaqItemRepository _items;
    private readonly IValidator<FaqItemRequestDto> _validator;

    public AdminFaqService(IFaqItemRepository items, IValidator<FaqItemRequestDto> validator)
    {
        _items = items;
        _validator = validator;
    }

    public async Task<IReadOnlyList<FaqItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _items.GetOrderedAsync(false, cancellationToken)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<FaqItemDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        => (await _items.GetOrderedAsync(true, cancellationToken)).Select(ToDto).ToList();

    public async Task<FaqItemDto> GetAsync(int id, CancellationToken cancellationToken = default)
        => ToDto(await FindAsync(id, cancellationToken));

    public async Task<FaqItemDto> CreateAsync(FaqItemRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var item = new FaqItem();
        Apply(request, item);
        item.SortOrder = request.SortOrder ?? await _items.NextSortOrderAsync(cancellationToken);
        await _items.AddAsync(item, cancellationToken);
        await _items.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<FaqItemDto> UpdateAsync(int id, FaqItemRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var item = await FindAsync(id, cancellationToken);
        Apply(request, item);
        if (request.SortOrder is not null)
        {
            item.SortOrder = request.SortOrder.Value;
        }

        _items.Update(item);
        await _items.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(id, cancellationToken);
        _items.Delete(item);
        await _items.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(FaqItemRequestDto request, FaqItem item)
    {
        item.Question = request.Question.Trim();
        item.Answer = request.Answer.Trim();
        item.IsActive = request.IsActive;
    }

    private async Task<FaqItem> FindAsync(int id, CancellationToken cancellationToken)
        => await _items.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Sual tapılmadı.");

    private static FaqItemDto ToDto(FaqItem item) => new(item.Id, item.Question, item.Answer, item.SortOrder, item.IsActive);
}
