namespace Domain.Enums;

public enum EmailTokenPurpose
{
    VerifyEmail,
    ResetPassword,

    /// <summary>The six-digit code of a password reset: the person types it in, and a correct one is traded for a <see cref="ResetPassword"/> ticket.</summary>
    ResetCode,

    /// <summary>The link that confirms one booking by e-mail before it can be paid.</summary>
    BookingConfirm,

    /// <summary>The link that confirms the cancellation of one paid booking; the booking is only cancelled when it is opened and confirmed.</summary>
    CancelBooking,

    /// <summary>The link that confirms the deletion of an account; the account is only erased when its page is opened and confirmed.</summary>
    DeleteAccount,

    /// <summary>The six-digit code sent to a NEW address to prove it belongs to the account owner; the address is only switched when it is typed in. The new address is kept in the payload.</summary>
    ChangeEmail
}
