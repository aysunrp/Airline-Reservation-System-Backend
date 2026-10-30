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
    }
}
