using Domain.Entities;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Passenger;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class ProfileService : IProfileService
{
    private readonly IUserRepository _userRepository;
    private readonly ISavedPassengerRepository _savedPassengerRepository;
    private readonly IValidator<UpdateProfileRequestDto> _profileValidator;
    private readonly IValidator<SavedPassengerRequestDto> _passengerValidator;

    public ProfileService(
        IUserRepository userRepository,
        ISavedPassengerRepository savedPassengerRepository,
        IValidator<UpdateProfileRequestDto> profileValidator,
        IValidator<SavedPassengerRequestDto> passengerValidator)
    {
        _userRepository = userRepository;
        _savedPassengerRepository = savedPassengerRepository;
        _profileValidator = profileValidator;
        _passengerValidator = passengerValidator;
    }

    public async Task<ProfileDto> GetProfileAsync(int userId, CancellationToken cancellationToken = default)
        => (await GetUserAsync(userId, cancellationToken)).ToProfileDto();

    public async Task<ProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        await _profileValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await GetUserAsync(userId, cancellationToken);

        // The address of the account is not changed here: a new address only becomes the account's after the code sent to it is typed in,
        // which the Account settings page does (api/account/email-change). The same address as now is fine, so the profile form can send it.
        var email = AuthService.NormalizeEmail(request.Email);
        if (email != user.Email)
        {
            throw new BusinessRuleException("E-poçt ünvanını Hesab tənzimləmələri səhifəsindən, yeni ünvana gələn kodla dəyişin.", "email_change_via_settings");
        }

        user.Title = null;
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.DateOfBirth = request.DateOfBirth;
        user.NationalityCode = request.NationalityCode;
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Replace(" ", string.Empty);

        // Left alone when not sent (an older page does not know these fields); sent empty, they are removed.
        if (request.FatherName is not null)
        {
            user.FatherName = string.IsNullOrWhiteSpace(request.FatherName) ? null : request.FatherName.Trim();
        }

        if (request.PassportNumber is not null)
        {
            user.PassportNumber = string.IsNullOrWhiteSpace(request.PassportNumber) ? null : DocumentNumbers.Normalize(request.PassportNumber);
        }

        if (request.NationalIdNumber is not null)
        {
            user.NationalIdNumber = string.IsNullOrWhiteSpace(request.NationalIdNumber) ? null : DocumentNumbers.Normalize(request.NationalIdNumber);
        }

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return user.ToProfileDto();
    }

    public async Task<IReadOnlyList<SavedPassengerDto>> GetSavedPassengersAsync(int userId, CancellationToken cancellationToken = default)
    {
        var passengers = await _savedPassengerRepository.GetByUserIdAsync(userId, cancellationToken);
        return passengers.Select(p => p.ToDto()).ToList();
    }

    public async Task<SavedPassengerDto> AddSavedPassengerAsync(int userId, SavedPassengerRequestDto request, CancellationToken cancellationToken = default)
    {
        await _passengerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var passenger = new SavedPassenger { UserId = userId };
        request.ApplyTo(passenger);

        await _savedPassengerRepository.AddAsync(passenger, cancellationToken);
        await _savedPassengerRepository.SaveChangesAsync(cancellationToken);

        return passenger.ToDto();
    }

    public async Task<SavedPassengerDto> UpdateSavedPassengerAsync(int userId, int passengerId, SavedPassengerRequestDto request, CancellationToken cancellationToken = default)
    {
        await _passengerValidator.ValidateAndThrowAsync(request, cancellationToken);
        var passenger = await GetOwnedPassengerAsync(userId, passengerId, cancellationToken);

        request.ApplyTo(passenger);
        _savedPassengerRepository.Update(passenger);
        await _savedPassengerRepository.SaveChangesAsync(cancellationToken);

        return passenger.ToDto();
    }

    public async Task RemoveSavedPassengerAsync(int userId, int passengerId, CancellationToken cancellationToken = default)
    {
        var passenger = await GetOwnedPassengerAsync(userId, passengerId, cancellationToken);

        _savedPassengerRepository.Delete(passenger);
        await _savedPassengerRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetUserAsync(int userId, CancellationToken cancellationToken)
        => await _userRepository.GetByIdAsync(userId, cancellationToken)
           ?? throw new NotFoundException("İstifadəçi tapılmadı.");

    // Another user's passenger is reported as "not found" so ids cannot be probed.
    private async Task<SavedPassenger> GetOwnedPassengerAsync(int userId, int passengerId, CancellationToken cancellationToken)
    {
        var passenger = await _savedPassengerRepository.GetByIdAsync(passengerId, cancellationToken);
        return passenger is not null && passenger.UserId == userId
            ? passenger
            : throw new NotFoundException("Saxlanılmış sərnişin tapılmadı.");
    }
}
