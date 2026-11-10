using Domain.Entities;
using Domain.Enums;

namespace Repository.Repositories.Interfaces;

public interface ISupportTicketRepository : IBaseRepository<SupportTicket>
{
    /// <summary>The ticket with its messages (oldest first) and its customer, tracked; null when there is none.</summary>
    Task<SupportTicket?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> NumberExistsAsync(string ticketNumber, CancellationToken cancellationToken = default);

    /// <summary>The customer's tickets, most recently active first (messages not loaded, with the number of messages).</summary>
    Task<IReadOnlyList<(SupportTicket Ticket, int MessageCount)>> GetForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>How many of the customer's tickets are not resolved.</summary>
    Task<int> CountOpenForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>A ticket of this customer with the same subject created after <paramref name="since"/> (a request sent twice), with its first message.</summary>
    Task<SupportTicket?> FindRecentDuplicateAsync(int userId, string subject, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>Staff list: filtered, newest activity first, one page; returns the total of the filter too.</summary>
    Task<(IReadOnlyList<(SupportTicket Ticket, int MessageCount)> Items, int Total)> SearchAsync(
        string? search, SupportStatus? status, SupportCategory? category, int page, int pageSize, CancellationToken cancellationToken = default);
}
