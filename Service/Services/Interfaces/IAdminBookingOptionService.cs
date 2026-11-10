using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

/// <summary>Admin management of the baggage and meal options offered during booking.</summary>
public interface IAdminBookingOptionService
{
    Task<IReadOnlyList<BaggageOptionDto>> GetBaggageOptionsAsync(CancellationToken cancellationToken = default);
    Task<BaggageOptionDto> GetBaggageOptionAsync(int id, CancellationToken cancellationToken = default);
    Task<BaggageOptionDto> CreateBaggageOptionAsync(BaggageOptionRequestDto request, CancellationToken cancellationToken = default);
    Task<BaggageOptionDto> UpdateBaggageOptionAsync(int id, BaggageOptionRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteBaggageOptionAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealOptionDto>> GetMealOptionsAsync(CancellationToken cancellationToken = default);
    Task<MealOptionDto> GetMealOptionAsync(int id, CancellationToken cancellationToken = default);
    Task<MealOptionDto> CreateMealOptionAsync(MealOptionRequestDto request, CancellationToken cancellationToken = default);
    Task<MealOptionDto> UpdateMealOptionAsync(int id, MealOptionRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteMealOptionAsync(int id, CancellationToken cancellationToken = default);
}
