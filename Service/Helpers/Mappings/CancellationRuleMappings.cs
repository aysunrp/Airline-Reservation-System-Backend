using Domain.Entities;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Mappings;

public static class CancellationRuleMappings
{
    public static CancellationRuleDto ToDto(this CancellationRule rule)
        => new(rule.Id, rule.CabinClass, rule.MinDaysBeforeDeparture, rule.FeePercent);
}
