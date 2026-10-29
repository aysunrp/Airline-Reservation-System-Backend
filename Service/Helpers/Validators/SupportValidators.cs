using FluentValidation;
using Service.Helpers.DTOs.Support;

namespace Service.Helpers.Validators;

public class CreateSupportTicketRequestValidator : AbstractValidator<CreateSupportTicketRequestDto>
{
    public const int MaxSubject = 120;
    public const int MaxMessage = 4000;

    public CreateSupportTicketRequestValidator()
    {
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(MaxSubject);
        RuleFor(x => x.Message).NotEmpty().MinimumLength(10).WithMessage("Please describe the problem in a few words (at least 10 characters).").MaximumLength(MaxMessage);
        RuleFor(x => x.BookingPnr).Matches("^[A-Za-z0-9]{5,10}$").WithMessage("A booking reference has 5 to 10 letters and digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.BookingPnr));
    }
}

public class SupportReplyRequestValidator : AbstractValidator<SupportReplyRequestDto>
{
    public SupportReplyRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(CreateSupportTicketRequestValidator.MaxMessage);
    }
}

public class AdminSupportReplyRequestValidator : AbstractValidator<AdminSupportReplyRequestDto>
{
    public AdminSupportReplyRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(CreateSupportTicketRequestValidator.MaxMessage);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
    }
}

public class SupportStatusRequestValidator : AbstractValidator<SupportStatusRequestDto>
{
    public SupportStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
