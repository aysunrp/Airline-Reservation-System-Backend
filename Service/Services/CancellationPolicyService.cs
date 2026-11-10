using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace Service.Services;

public class CancellationPolicyService : ICancellationPolicyService
{
    private readonly ICancellationRuleRepository _ruleRepository;
    private readonly TimeProvider _timeProvider;

    public CancellationPolicyService(ICancellationRuleRepository ruleRepository, TimeProvider timeProvider)
    {
        _ruleRepository = ruleRepository;
        _timeProvider = timeProvider;
    }

    public async Task<CancellationQuoteDto> GetQuoteAsync(CabinClass cabinClass, DateTime departureAt, decimal amountPaid, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var daysBeforeDeparture = DateOnly.FromDateTime(departureAt).DayNumber - today.DayNumber;

        var rules = await _ruleRepository.GetByCabinClassAsync(cabinClass, cancellationToken);
        var rule = rules
            .OrderByDescending(r => r.MinDaysBeforeDeparture)
            .FirstOrDefault(r => daysBeforeDeparture >= r.MinDaysBeforeDeparture);

        // No matching band, or a band without a fee percent, means the booking can be cancelled but not refunded.
        if (rule?.FeePercent is not decimal feePercent)
        {
            return new CancellationQuoteDto(daysBeforeDeparture, amountPaid, null, amountPaid, 0m, false);
        }

        var fee = Math.Round(amountPaid * feePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new CancellationQuoteDto(daysBeforeDeparture, amountPaid, feePercent, fee, amountPaid - fee, true);
    }
}
