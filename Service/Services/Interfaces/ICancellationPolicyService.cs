using Domain.Enums;
using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface ICancellationPolicyService
{
    /// <summary>Calculates the cancellation fee and refund from the cabin's fare rules and the days left until departure.</summary>
    Task<CancellationQuoteDto> GetQuoteAsync(CabinClass cabinClass, DateTime departureAt, decimal amountPaid, CancellationToken cancellationToken = default);
}
