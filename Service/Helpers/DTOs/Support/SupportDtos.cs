using Domain.Enums;

namespace Service.Helpers.DTOs.Support;

public class CreateSupportTicketRequestDto
{
    public SupportCategory Category { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional booking reference (PNR).</summary>
    public string? BookingPnr { get; set; }
}

public class SupportReplyRequestDto
{
    public string Message { get; set; } = string.Empty;
}

public class AdminSupportReplyRequestDto
{
    public string Message { get; set; } = string.Empty;

    /// <summary>The status after the reply; empty means "waiting for the customer".</summary>
    public SupportStatus? Status { get; set; }
}

public class SupportStatusRequestDto
{
    public SupportStatus Status { get; set; }
}

public record SupportTicketDto(
    int Id,
    string TicketNumber,
    string Subject,
    SupportCategory Category,
    SupportStatus Status,
    string? BookingPnr,
    int MessageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SupportMessageDto(int Id, SupportAuthorType AuthorType, string AuthorName, string Body, DateTime CreatedAt);

public record SupportTicketDetailDto(
    int Id,
    string TicketNumber,
    string Subject,
    SupportCategory Category,
    SupportStatus Status,
    string? BookingPnr,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    /// <summary>False when the request is resolved: the customer opens a new one instead.</summary>
    bool CanReply,
    IReadOnlyList<SupportMessageDto> Messages);

public record AdminSupportTicketDto(
    int Id,
    string TicketNumber,
    string Subject,
    SupportCategory Category,
    SupportStatus Status,
    string? BookingPnr,
    string CustomerName,
    string CustomerEmail,
    int MessageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record AdminSupportTicketDetailDto(
    int Id,
    string TicketNumber,
    string Subject,
    SupportCategory Category,
    SupportStatus Status,
    string? BookingPnr,
    string CustomerName,
    string CustomerEmail,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<SupportMessageDto> Messages);

public record AdminSupportPageDto(IReadOnlyList<AdminSupportTicketDto> Items, int Total, int Page, int PageSize);

/// <summary>What the Support page may show about how to reach AEROVA: only what is configured.</summary>
public record SupportInfoDto(string? ContactEmail);
