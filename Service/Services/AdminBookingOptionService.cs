using Domain.Entities;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminBookingOptionService : IAdminBookingOptionService
{
    private const string InUseMessage = "This option was sold with tickets. Set it to inactive instead of deleting it.";

    private readonly IBaggageOptionRepository _baggageRepository;
    private readonly IMealOptionRepository _mealRepository;
    private readonly IValidator<BaggageOptionRequestDto> _baggageValidator;
    private readonly IValidator<MealOptionRequestDto> _mealValidator;

    public AdminBookingOptionService(
        IBaggageOptionRepository baggageRepository,
        IMealOptionRepository mealRepository,
        IValidator<BaggageOptionRequestDto> baggageValidator,
        IValidator<MealOptionRequestDto> mealValidator)
    {
        _baggageRepository = baggageRepository;
        _mealRepository = mealRepository;
        _baggageValidator = baggageValidator;
        _mealValidator = mealValidator;
    }

    // Inactive options are listed too, cheapest first like on the booking pages.
    public async Task<IReadOnlyList<BaggageOptionDto>> GetBaggageOptionsAsync(CancellationToken cancellationToken = default)
        => (await _baggageRepository.GetAllAsync(cancellationToken)).OrderBy(o => o.Price).ThenBy(o => o.Code).Select(o => o.ToDto()).ToList();

    public async Task<BaggageOptionDto> GetBaggageOptionAsync(int id, CancellationToken cancellationToken = default)
        => (await GetBaggageAsync(id, cancellationToken)).ToDto();

    public async Task<BaggageOptionDto> CreateBaggageOptionAsync(BaggageOptionRequestDto request, CancellationToken cancellationToken = default)
    {
        await _baggageValidator.ValidateAndThrowAsync(request, cancellationToken);
        var code = Normalize(request.Code);
        await EnsureFreeAsync(await _baggageRepository.CodeExistsAsync(code, null, cancellationToken), code);

        var option = new BaggageOption();
        Apply(request, code, option);
        await _baggageRepository.AddAsync(option, cancellationToken);
        await _baggageRepository.SaveChangesAsync(cancellationToken);
        return option.ToDto();
    }

    public async Task<BaggageOptionDto> UpdateBaggageOptionAsync(int id, BaggageOptionRequestDto request, CancellationToken cancellationToken = default)
    {
        await _baggageValidator.ValidateAndThrowAsync(request, cancellationToken);
        var option = await GetBaggageAsync(id, cancellationToken);
        var code = Normalize(request.Code);
        await EnsureFreeAsync(await _baggageRepository.CodeExistsAsync(code, id, cancellationToken), code);

        // Sold tickets keep the price they were charged, so changing the price only affects new bookings.
        Apply(request, code, option);
        _baggageRepository.Update(option);
        await _baggageRepository.SaveChangesAsync(cancellationToken);
        return option.ToDto();
    }

    public async Task DeleteBaggageOptionAsync(int id, CancellationToken cancellationToken = default)
    {
        var option = await GetBaggageAsync(id, cancellationToken);
        if (await _baggageRepository.IsInUseAsync(id, cancellationToken))
        {
            throw new BusinessRuleException(InUseMessage);
        }

        _baggageRepository.Delete(option);
        await _baggageRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MealOptionDto>> GetMealOptionsAsync(CancellationToken cancellationToken = default)
        => (await _mealRepository.GetAllAsync(cancellationToken)).OrderBy(o => o.Price).ThenBy(o => o.Code).Select(o => o.ToDto()).ToList();

    public async Task<MealOptionDto> GetMealOptionAsync(int id, CancellationToken cancellationToken = default)
        => (await GetMealAsync(id, cancellationToken)).ToDto();

    public async Task<MealOptionDto> CreateMealOptionAsync(MealOptionRequestDto request, CancellationToken cancellationToken = default)
    {
        await _mealValidator.ValidateAndThrowAsync(request, cancellationToken);
        var code = Normalize(request.Code);
        await EnsureFreeAsync(await _mealRepository.CodeExistsAsync(code, null, cancellationToken), code);

        var option = new MealOption();
        Apply(request, code, option);
        await _mealRepository.AddAsync(option, cancellationToken);
        await _mealRepository.SaveChangesAsync(cancellationToken);
        return option.ToDto();
    }

    public async Task<MealOptionDto> UpdateMealOptionAsync(int id, MealOptionRequestDto request, CancellationToken cancellationToken = default)
    {
        await _mealValidator.ValidateAndThrowAsync(request, cancellationToken);
        var option = await GetMealAsync(id, cancellationToken);
        var code = Normalize(request.Code);
        await EnsureFreeAsync(await _mealRepository.CodeExistsAsync(code, id, cancellationToken), code);

        Apply(request, code, option);
        _mealRepository.Update(option);
        await _mealRepository.SaveChangesAsync(cancellationToken);
        return option.ToDto();
    }

    public async Task DeleteMealOptionAsync(int id, CancellationToken cancellationToken = default)
    {
        var option = await GetMealAsync(id, cancellationToken);
        if (await _mealRepository.IsInUseAsync(id, cancellationToken))
        {
            throw new BusinessRuleException(InUseMessage);
        }

        _mealRepository.Delete(option);
        await _mealRepository.SaveChangesAsync(cancellationToken);
    }

    private static string Normalize(string code) => code.Trim().ToLowerInvariant();

    private static Task EnsureFreeAsync(bool exists, string code)
        => exists ? throw new ConflictException($"An option with the code {code} already exists.") : Task.CompletedTask;

    private static void Apply(BaggageOptionRequestDto request, string code, BaggageOption option)
    {
        option.Code = code;
        option.Name = request.Name.Trim();
        option.WeightKg = request.WeightKg;
        option.Price = request.Price;
        option.IsActive = request.IsActive;
    }

    private static void Apply(MealOptionRequestDto request, string code, MealOption option)
    {
        option.Code = code;
        option.Name = request.Name.Trim();
        option.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        option.Price = request.Price;
        option.IsActive = request.IsActive;
    }

    private async Task<BaggageOption> GetBaggageAsync(int id, CancellationToken cancellationToken)
        => await _baggageRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Baggage option not found.");

    private async Task<MealOption> GetMealAsync(int id, CancellationToken cancellationToken)
        => await _mealRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Meal option not found.");
}
