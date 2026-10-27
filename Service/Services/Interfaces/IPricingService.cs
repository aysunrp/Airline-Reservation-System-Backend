using Domain.Enums;
using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface IPricingService
{
    /// <summary>Resolves Infant/Child/Adult from the passenger's age on the travel date.</summary>
    PassengerType GetPassengerType(DateOnly dateOfBirth, DateOnly travelDate);

    /// <summary>The age limits and the passenger limit the server applies, for the pages to show the same rules.</summary>
    PassengerRulesDto GetPassengerRules();

    /// <summary>The share of the cabin fare a passenger of the given type pays.</summary>
    decimal GetPassengerFare(decimal cabinFare, PassengerType passengerType);

    /// <summary>
    /// Builds the booking total the same way the payment page does:
    /// taxes on the base fare, subtotal with add-ons, then the promo discount on the subtotal.
    /// </summary>
    PriceBreakdownDto Calculate(decimal baseFare, decimal baggage, decimal meals, PromoCodeDto? promoCode);
}
