using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Refund : BaseEntity
{
    public int CancellationId { get; set; }
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public RefundStatus Status { get; set; } = RefundStatus.Pending;
    public string? ProviderReference { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public Cancellation Cancellation { get; set; } = null!;
    public Payment Payment { get; set; } = null!;
}
