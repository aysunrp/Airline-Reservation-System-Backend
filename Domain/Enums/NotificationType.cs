namespace Domain.Enums;

public enum NotificationType
{
    BookingConfirmed,
    BookingCancelled,
    PaymentSuccessful,
    FlightUpdate,
    CheckInReminder,
    RefundCompleted,

    /// <summary>Stripe has taken the refund and is still processing it: nothing is said to have arrived yet.</summary>
    RefundPending,

    /// <summary>The refund did not go through; the booking stays cancelled and the amount is still owed.</summary>
    RefundFailed,

    /// <summary>A support request was received, answered or changed status.</summary>
    SupportUpdate
}
