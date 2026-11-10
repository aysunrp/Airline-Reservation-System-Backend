namespace Service.Services.Interfaces;

public sealed record RefundProcessingResult(int Processing, int Completed);

public interface IRefundProcessingService
{
    /// <summary>Moves requested refunds through the mock provider: Pending, then Processing, then Completed.</summary>
    Task<RefundProcessingResult> ProcessDueAsync(CancellationToken cancellationToken = default);
}
