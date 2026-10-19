using FluentValidation;
using Service.Helpers.DTOs.Airport;

namespace Service.Helpers.Validators;

public class AirportRequestValidator : AbstractValidator<AirportRequestDto>
{
    public AirportRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\s*[A-Za-z]{3}\s*$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CountryCode).NotEmpty().Matches(@"^\s*[A-Za-z]{2}\s*$").WithMessage("Use a two-letter country code.");
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(64)
            .Must(BeAnIanaTimeZone).WithMessage("Use an IANA time zone such as Asia/Baku or Europe/London.");
    }

    // Windows names such as "Azerbaijan Standard Time" also resolve on Windows, so they are ruled out explicitly.
    private static bool BeAnIanaTimeZone(string? id)
    {
        var value = id?.Trim();
        return !string.IsNullOrEmpty(value)
               && TimeZoneInfo.TryFindSystemTimeZoneById(value, out _)
               && !TimeZoneInfo.TryConvertWindowsIdToIanaId(value, out _);
    }
}
