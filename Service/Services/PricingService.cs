using Domain.Enums;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class PricingService : IPricingService
{
    private readonly PricingSettings _settings;

    public PricingService(IOptions<PricingSettings> settings)
    {
        _settings = settings.Value;
    }

    public PassengerType GetPassengerType(DateOnly dateOfBirth, DateOnly travelDate)
    {
        if (dateOfBirth > travelDate)
        {
            throw new BusinessRuleException("Date of birth cannot be after the travel date.");
        }

        var age = travelDate.Year - dateOfBirth.Year;
        if (dateOfBirth > travelDate.AddYears(-age))
        {
            age--;
        }

        if (age < _settings.InfantMaxAgeYears)
        {
            return PassengerType.Infant;
        }

        return age < _settings.ChildMaxAgeYears ? PassengerType.Child : PassengerType.Adult;
    }

    public decimal GetPassengerFare(decimal cabinFare, PassengerType passengerType)
    {
        var rate = passengerType switch
        {
            PassengerType.Infant => _settings.InfantFareRate,
            PassengerType.Child => _settings.ChildFareRate,
            _ => 1m
        };

        return Round(cabinFare * rate);
    }

    public PriceBreakdownDto Calculate(decimal baseFare, decimal baggage, decimal meals, PromoCodeDto? promoCode)
    {
        baseFare = Round(baseFare);
        baggage = Round(baggage);
        meals = Round(meals);

        var taxes = Round(baseFare * _settings.TaxRate);
        var subtotal = Round(baseFare + baggage + meals + taxes);
        var promoPercent = promoCode?.DiscountPercent ?? 0;
        var discount = Round(subtotal * promoPercent / 100m);

        return new PriceBreakdownDto(
            baseFare, baggage, meals, taxes, subtotal,
            promoCode?.Code, promoPercent, discount, subtotal - discount);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
