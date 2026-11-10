using FluentValidation;
using Service.Helpers.DTOs.Destinations;

namespace Service.Helpers.Validators;

public class DestinationCountryRequestValidator : AbstractValidator<DestinationCountryRequestDto>
{
    public DestinationCountryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameAz).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
        RuleFor(x => x.DescriptionAz).NotEmpty().MaximumLength(400);
        RuleFor(x => x.DescriptionRu).NotEmpty().MaximumLength(400);
    }
}

public class DestinationCityRequestValidator : AbstractValidator<DestinationCityRequestDto>
{
    public DestinationCityRequestValidator()
    {
        RuleFor(x => x.NameAz).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Aliases).MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
        RuleFor(x => x.DescriptionAz).NotEmpty().MaximumLength(400);
        RuleFor(x => x.DescriptionRu).NotEmpty().MaximumLength(400);
    }
}

public class DestinationCountryCreateValidator : AbstractValidator<DestinationCountryCreateDto>
{
    public DestinationCountryCreateValidator()
    {
        Include(new DestinationCountryRequestValidator());
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\s*[A-Za-z]{2}\s*$").WithMessage("Use a two-letter country code.");
    }
}

public class DestinationCityCreateValidator : AbstractValidator<DestinationCityCreateDto>
{
    public DestinationCityCreateValidator()
    {
        Include(new DestinationCityRequestValidator());
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AirportCode).NotEmpty().Matches(@"^\s*[A-Za-z]{3}\s*$").WithMessage("Use a three-letter airport code.");
        RuleFor(x => x.AirportName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(64)
            .Must(BeAnIanaTimeZone).WithMessage("Use an IANA time zone such as Asia/Baku or Europe/London.");
    }

    // The same rule as an airport created on its own: a Windows name such as "Azerbaijan Standard Time" is not accepted.
    private static bool BeAnIanaTimeZone(string? id)
    {
        var value = id?.Trim();
        return !string.IsNullOrEmpty(value)
               && TimeZoneInfo.TryFindSystemTimeZoneById(value, out _)
               && !TimeZoneInfo.TryConvertWindowsIdToIanaId(value, out _);
    }
}
