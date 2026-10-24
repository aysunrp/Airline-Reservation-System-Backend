namespace Service.Services.Interfaces;

/// <summary>
/// Counts failed sign-ins per e-mail address and pauses sign-in after too many. The count follows the address that was typed,
/// whether or not an account exists, so the pause does not tell anyone which addresses have accounts.
/// </summary>
public interface ILoginAttemptTracker
{
    /// <summary>True (with the time left) while sign-in is paused for the address.</summary>
    bool IsLockedOut(string email, out TimeSpan retryAfter);

    /// <summary>Counts a failed sign-in; returns true when this failure paused sign-in.</summary>
    bool RecordFailure(string email);

    /// <summary>Forgets the failures after a successful sign-in.</summary>
    void Reset(string email);
}
