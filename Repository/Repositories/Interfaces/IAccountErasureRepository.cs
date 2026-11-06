namespace Repository.Repositories.Interfaces;

/// <summary>What deleting a customer account needs from the database. Bookings, payments, refunds and the audit log are never touched.</summary>
public interface IAccountErasureRepository
{
    /// <summary>
    /// True when the account still has something running: a held (unpaid) booking, a paid booking whose flight has not left yet, or a
    /// refund that is not finished. Such an account is not deleted until that is over.
    /// </summary>
    Task<bool> HasOpenTripsAsync(int userId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks for removal (saved with the next save of the same unit of work) the personal data kept for the account: saved passengers,
    /// notifications, support requests with their messages, every e-mail token except <paramref name="keepTokenId"/>, and the newsletter
    /// subscription of <paramref name="email"/>.
    /// </summary>
    Task StageErasureAsync(int userId, string email, int keepTokenId, CancellationToken cancellationToken = default);
}
