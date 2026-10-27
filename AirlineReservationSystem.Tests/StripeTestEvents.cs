using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// Stripe events the way Stripe sends them: a JSON body and a Stripe-Signature header (t=timestamp,v1=HMAC-SHA256 of
/// "timestamp.body" with the webhook secret). Made up for tests; nothing here talks to Stripe.
/// </summary>
internal static class StripeTestEvents
{
    public static string Sign(string payload, string secret, DateTimeOffset? at = null)
    {
        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"))).ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }

    public static string CheckoutSession(
        string eventId,
        string type,
        string sessionId,
        int paymentId,
        int bookingId,
        long amountTotal,
        string currency = "usd",
        string paymentStatus = "paid",
        string? paymentIntent = "pi_test_0001",
        int? metadataPaymentId = null)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = eventId,
            ["object"] = "event",
            ["api_version"] = "2025-01-27.acacia",
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["livemode"] = false,
            ["type"] = type,
            ["data"] = new Dictionary<string, object?>
            {
                ["object"] = new Dictionary<string, object?>
                {
                    ["id"] = sessionId,
                    ["object"] = "checkout.session",
                    ["mode"] = "payment",
                    ["amount_total"] = amountTotal,
                    ["currency"] = currency,
                    ["payment_status"] = paymentStatus,
                    ["status"] = paymentStatus == "paid" ? "complete" : "open",
                    ["payment_intent"] = paymentIntent,
                    ["client_reference_id"] = paymentId.ToString(),
                    ["metadata"] = new Dictionary<string, string>
                    {
                        ["payment_id"] = (metadataPaymentId ?? paymentId).ToString(),
                        ["booking_id"] = bookingId.ToString(),
                        ["pnr"] = "TEST01"
                    }
                }
            }
        });

    public static string Other(string eventId, string type)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = eventId,
            ["object"] = "event",
            ["api_version"] = "2025-01-27.acacia",
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["livemode"] = false,
            ["type"] = type,
            ["data"] = new Dictionary<string, object?>
            {
                ["object"] = new Dictionary<string, object?> { ["id"] = "cus_test_0001", ["object"] = "customer" }
            }
        });
}
