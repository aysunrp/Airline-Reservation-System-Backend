using Domain.Enums;

namespace Service.Helpers.DTOs.Reservation;

/// <summary>One refund as the admin panel lists it. The Stripe id is a reference, not a secret.</summary>
public record AdminRefundDto(
    int Id,
    string Pnr,
    decimal Amount,
    string Currency,
    RefundStatus Status,
    string? Reason,
    string? FailureReason,
    string? ProviderReference,
    string? ProviderStatus,
    decimal AmountPaid,
    decimal FeeAmount,
    int Attempts,
    string InitiatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ProcessedAt);
