namespace Domain.Enums;

public enum SupportCategory
{
    Booking,
    Payment,
    CancellationRefund,
    FlightChange,
    Baggage,
    Account,
    WebsiteProblem,
    Other
}

public enum SupportStatus
{
    /// <summary>Waiting for support staff (a new request, or the customer wrote again).</summary>
    Open,

    InProgress,

    /// <summary>Support asked something and waits for the customer.</summary>
    WaitingForCustomer,

    /// <summary>Done. The customer cannot reply to a resolved request (a new one is opened instead); staff can still reopen it.</summary>
    Resolved
}

public enum SupportAuthorType
{
    Customer,
    Staff
}
