namespace Service.Services.Interfaces;

public interface IBookingExpiryService
{
    /// <summary>Expires unpaid bookings whose hold has run out, cancels their tickets and frees their seats. Returns the number expired.</summary>
    Task<int> ReleaseExpiredAsync(CancellationToken cancellationToken = default);
}
