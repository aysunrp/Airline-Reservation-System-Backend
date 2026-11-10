using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Passenger;

namespace Service.Services.Interfaces;

/// <summary>Profile page: the signed-in user's details and saved passengers. The userId comes from the token.</summary>
public interface IProfileService
{
    Task<ProfileDto> GetProfileAsync(int userId, CancellationToken cancellationToken = default);

    Task<ProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedPassengerDto>> GetSavedPassengersAsync(int userId, CancellationToken cancellationToken = default);

    Task<SavedPassengerDto> AddSavedPassengerAsync(int userId, SavedPassengerRequestDto request, CancellationToken cancellationToken = default);

    Task<SavedPassengerDto> UpdateSavedPassengerAsync(int userId, int passengerId, SavedPassengerRequestDto request, CancellationToken cancellationToken = default);

    Task RemoveSavedPassengerAsync(int userId, int passengerId, CancellationToken cancellationToken = default);
}
