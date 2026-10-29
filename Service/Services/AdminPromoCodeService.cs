using Domain.Entities;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminPromoCodeService : IAdminPromoCodeService
{
    private readonly IPromoCodeRepository _promoCodeRepository;
    private readonly IValidator<PromoCodeRequestDto> _validator;

    public AdminPromoCodeService(IPromoCodeRepository promoCodeRepository, IValidator<PromoCodeRequestDto> validator)
    {
        _promoCodeRepository = promoCodeRepository;
        _validator = validator;
    }

    public async Task<IReadOnlyList<AdminPromoCodeDto>> GetAllAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim();
        return (await _promoCodeRepository.GetAllAsync(cancellationToken))
            .Where(p => isActive is null || p.IsActive == isActive)
            .Where(p => string.IsNullOrEmpty(term) || p.Code.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Code)
            .Select(p => p.ToAdminDto())
            .ToList();
    }

    public async Task<AdminPromoCodeDto> GetAsync(int id, CancellationToken cancellationToken = default)
        => (await GetPromoCodeAsync(id, cancellationToken)).ToAdminDto();

    public async Task<AdminPromoCodeDto> CreateAsync(PromoCodeRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var code = Normalize(request.Code);
        await EnsureCodeIsFreeAsync(code, null, cancellationToken);

        var promoCode = new PromoCode();
        Apply(request, code, promoCode);

        await _promoCodeRepository.AddAsync(promoCode, cancellationToken);
        await _promoCodeRepository.SaveChangesAsync(cancellationToken);
        return promoCode.ToAdminDto();
    }

    public async Task<AdminPromoCodeDto> UpdateAsync(int id, PromoCodeRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var promoCode = await GetPromoCodeAsync(id, cancellationToken);
        var code = Normalize(request.Code);
        await EnsureCodeIsFreeAsync(code, id, cancellationToken);

        // The usage counter is only ever moved by paid bookings, so a limit below it could never be met.
        if (request.MaxUses is int maxUses && maxUses < promoCode.UsedCount)
        {
            throw new BusinessRuleException($"This code has already been used {promoCode.UsedCount} times, so the limit cannot be lower.");
        }

        Apply(request, code, promoCode);
        _promoCodeRepository.Update(promoCode);
        await _promoCodeRepository.SaveChangesAsync(cancellationToken);
        return promoCode.ToAdminDto();
    }

    public async Task<DeleteOutcome> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var promoCode = await GetPromoCodeAsync(id, cancellationToken);
        if (promoCode.UsedCount > 0 || await _promoCodeRepository.IsInUseAsync(id, cancellationToken))
        {
            // Bookings were priced with the code: its history stays, the code just stops working.
            promoCode.IsActive = false;
            _promoCodeRepository.Update(promoCode);
            await _promoCodeRepository.SaveChangesAsync(cancellationToken);
            return DeleteOutcome.Deactivated;
        }

        _promoCodeRepository.Delete(promoCode);
        await _promoCodeRepository.SaveChangesAsync(cancellationToken);
        return DeleteOutcome.Deleted;
    }

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();

    // Times without a zone are treated as UTC, like every stored time.
    private static DateTime? AsUtc(DateTime? value)
        => value is DateTime time
            ? time.Kind switch
            {
                DateTimeKind.Utc => time,
                DateTimeKind.Local => time.ToUniversalTime(),
                _ => DateTime.SpecifyKind(time, DateTimeKind.Utc)
            }
            : null;

    private static void Apply(PromoCodeRequestDto request, string code, PromoCode promoCode)
    {
        promoCode.Code = code;
        promoCode.DiscountPercent = request.DiscountPercent;
        promoCode.ValidFrom = AsUtc(request.ValidFrom);
        promoCode.ValidTo = AsUtc(request.ValidTo);
        promoCode.MaxUses = request.MaxUses;
        promoCode.IsActive = request.IsActive;
    }

    private async Task EnsureCodeIsFreeAsync(string code, int? excludeId, CancellationToken cancellationToken)
    {
        if (await _promoCodeRepository.CodeExistsAsync(code, excludeId, cancellationToken))
        {
            throw new ConflictException($"The promo code {code} already exists.");
        }
    }

    private async Task<PromoCode> GetPromoCodeAsync(int id, CancellationToken cancellationToken)
        => await _promoCodeRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException("Promo code not found.");
}
