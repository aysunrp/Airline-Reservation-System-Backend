using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Admin management of the fare rules used for cancellation quotes. Changes only affect quotes and cancellations
/// made afterwards; cancellations already stored keep their fee and refund amounts.
/// </summary>
public class AdminCancellationRuleService : IAdminCancellationRuleService
{
    private readonly ICancellationRuleRepository _ruleRepository;
    private readonly IValidator<CancellationRuleRequestDto> _validator;

    public AdminCancellationRuleService(ICancellationRuleRepository ruleRepository, IValidator<CancellationRuleRequestDto> validator)
    {
        _ruleRepository = ruleRepository;
        _validator = validator;
    }

    public async Task<IReadOnlyList<CancellationRuleDto>> GetAllAsync(CabinClass? cabinClass = null, CancellationToken cancellationToken = default)
        => (await _ruleRepository.GetAllAsync(cancellationToken))
            .Where(r => cabinClass is null || r.CabinClass == cabinClass)
            .OrderBy(r => r.CabinClass)
            .ThenByDescending(r => r.MinDaysBeforeDeparture)
            .Select(r => r.ToDto())
            .ToList();

    public async Task<CancellationRuleDto> GetAsync(int id, CancellationToken cancellationToken = default)
        => (await GetRuleAsync(id, cancellationToken)).ToDto();

    public async Task<CancellationRuleDto> CreateAsync(CancellationRuleRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureBandIsFreeAsync(request, null, cancellationToken);

        var rule = new CancellationRule();
        Apply(request, rule);
        await _ruleRepository.AddAsync(rule, cancellationToken);
        await _ruleRepository.SaveChangesAsync(cancellationToken);
        return rule.ToDto();
    }

    public async Task<CancellationRuleDto> UpdateAsync(int id, CancellationRuleRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var rule = await GetRuleAsync(id, cancellationToken);
        await EnsureBandIsFreeAsync(request, id, cancellationToken);

        Apply(request, rule);
        _ruleRepository.Update(rule);
        await _ruleRepository.SaveChangesAsync(cancellationToken);
        return rule.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var rule = await GetRuleAsync(id, cancellationToken);
        _ruleRepository.Delete(rule);
        await _ruleRepository.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(CancellationRuleRequestDto request, CancellationRule rule)
    {
        rule.CabinClass = request.CabinClass;
        rule.MinDaysBeforeDeparture = request.MinDaysBeforeDeparture;
        rule.FeePercent = request.FeePercent;
    }

    private async Task EnsureBandIsFreeAsync(CancellationRuleRequestDto request, int? excludeId, CancellationToken cancellationToken)
    {
        if (await _ruleRepository.ExistsAsync(request.CabinClass, request.MinDaysBeforeDeparture, excludeId, cancellationToken))
        {
            throw new ConflictException($"{request.CabinClass} already has a rule from {request.MinDaysBeforeDeparture} days before departure.");
        }
    }

    private async Task<CancellationRule> GetRuleAsync(int id, CancellationToken cancellationToken)
        => await _ruleRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Cancellation rule not found.");
}
