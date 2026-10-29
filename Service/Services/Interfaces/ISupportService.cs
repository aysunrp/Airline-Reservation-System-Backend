using Domain.Enums;
using Service.Helpers.DTOs.Support;

namespace Service.Services.Interfaces;

/// <summary>Customer side of the support system. The customer is always the signed-in account: no id comes from the request.</summary>
public interface ISupportService
{
    SupportInfoDto GetInfo();

    Task<SupportTicketDetailDto> CreateAsync(int userId, CreateSupportTicketRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportTicketDto>> GetMineAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The ticket and its conversation; 404 for a ticket that is not the customer's (its existence is not revealed).</summary>
    Task<SupportTicketDetailDto> GetMineAsync(int userId, int ticketId, CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> ReplyAsync(int userId, int ticketId, SupportReplyRequestDto request, CancellationToken cancellationToken = default);
}

/// <summary>Staff side: every action is for an Admin or SuperAdmin account (checked by the controller policy).</summary>
public interface IAdminSupportService
{
    Task<AdminSupportPageDto> GetAsync(string? search, SupportStatus? status, SupportCategory? category, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AdminSupportTicketDetailDto> GetAsync(int ticketId, CancellationToken cancellationToken = default);

    Task<AdminSupportTicketDetailDto> ReplyAsync(int staffUserId, int ticketId, AdminSupportReplyRequestDto request, CancellationToken cancellationToken = default);

    Task<AdminSupportTicketDetailDto> SetStatusAsync(int ticketId, SupportStatusRequestDto request, CancellationToken cancellationToken = default);
}
