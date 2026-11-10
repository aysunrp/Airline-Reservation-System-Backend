using Domain.Entities;
using Service.Helpers.DTOs.Reservation;

namespace Service.Helpers.Mappings;

public static class ExtraOptionMappings
{
    public static BaggageOptionDto ToDto(this BaggageOption option)
        => new(option.Id, option.Code, option.Name, option.WeightKg, option.Price, option.IsActive);

    public static MealOptionDto ToDto(this MealOption option)
        => new(option.Id, option.Code, option.Name, option.Description, option.Price, option.IsActive);
}
