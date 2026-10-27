using System.Text.Json;

namespace AirlineReservationSystem.Tests;

/// <summary>Keeps the published API surface in line with the stage 6 plan: every endpoint exists and only the right ones need a token.</summary>
public class SwaggerContractTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SwaggerContractTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string, bool> Endpoints => new()
    {
        // Client: anonymous
        { "post", "/api/auth/register", false },
        { "post", "/api/auth/login", false },
        { "post", "/api/auth/verify-email", false },
        { "post", "/api/auth/forgot-password", false },
        { "post", "/api/auth/reset-password", false },
        { "get", "/api/airports", false },
        { "post", "/api/flights/search", false },
        { "get", "/api/flights/lowest-fares", false },
        { "get", "/api/flights/{id}", false },
        { "get", "/api/flights/{id}/seats", false },
        { "get", "/api/booking-options/baggage", false },
        { "get", "/api/booking-options/meals", false },
        { "get", "/api/booking-options/passenger-rules", false },
        { "get", "/api/promo-codes/{code}", false },
        { "post", "/api/bookings/quote", false },
        { "post", "/api/bookings", false },
        { "get", "/api/bookings/{pnr}", false },
        { "get", "/api/bookings/{pnr}/cancellation-quote", false },
        { "post", "/api/bookings/{pnr}/cancel", false },
        { "post", "/api/payments", false },
        // Client: signed in
        { "post", "/api/auth/resend-verification", true },
        { "get", "/api/profile", true },
        { "put", "/api/profile", true },
        { "get", "/api/profile/saved-passengers", true },
        { "post", "/api/profile/saved-passengers", true },
        { "put", "/api/profile/saved-passengers/{id}", true },
        { "delete", "/api/profile/saved-passengers/{id}", true },
        { "get", "/api/bookings/my", true },
        { "get", "/api/bookings/my/{pnr}", true },
        { "get", "/api/notifications", true },
        { "get", "/api/notifications/unread-count", true },
        { "patch", "/api/notifications/{id}/read", true },
        { "patch", "/api/notifications/read-all", true },
        { "delete", "/api/notifications/{id}", true },
        { "delete", "/api/notifications", true },
        // Admin
        { "get", "/api/admin/dashboard", true },
        { "get", "/api/admin/airports", true },
        { "post", "/api/admin/airports", true },
        { "get", "/api/admin/airports/{id}", true },
        { "put", "/api/admin/airports/{id}", true },
        { "delete", "/api/admin/airports/{id}", true },
        { "get", "/api/admin/promo-codes", true },
        { "post", "/api/admin/promo-codes", true },
        { "get", "/api/admin/promo-codes/{id}", true },
        { "put", "/api/admin/promo-codes/{id}", true },
        { "delete", "/api/admin/promo-codes/{id}", true },
        { "get", "/api/admin/booking-options/baggage", true },
        { "post", "/api/admin/booking-options/baggage", true },
        { "get", "/api/admin/booking-options/baggage/{id}", true },
        { "put", "/api/admin/booking-options/baggage/{id}", true },
        { "delete", "/api/admin/booking-options/baggage/{id}", true },
        { "get", "/api/admin/booking-options/meals", true },
        { "post", "/api/admin/booking-options/meals", true },
        { "get", "/api/admin/booking-options/meals/{id}", true },
        { "put", "/api/admin/booking-options/meals/{id}", true },
        { "delete", "/api/admin/booking-options/meals/{id}", true },
        { "get", "/api/admin/cancellation-rules", true },
        { "post", "/api/admin/cancellation-rules", true },
        { "get", "/api/admin/cancellation-rules/{id}", true },
        { "put", "/api/admin/cancellation-rules/{id}", true },
        { "delete", "/api/admin/cancellation-rules/{id}", true },
        { "get", "/api/admin/aircraft", true },
        { "post", "/api/admin/aircraft", true },
        { "get", "/api/admin/aircraft/{id}", true },
        { "put", "/api/admin/aircraft/{id}", true },
        { "delete", "/api/admin/aircraft/{id}", true },
        { "get", "/api/admin/routes", true },
        { "post", "/api/admin/routes", true },
        { "get", "/api/admin/routes/{id}", true },
        { "put", "/api/admin/routes/{id}", true },
        { "delete", "/api/admin/routes/{id}", true },
        { "get", "/api/admin/schedules", true },
        { "post", "/api/admin/schedules", true },
        { "get", "/api/admin/schedules/{id}", true },
        { "put", "/api/admin/schedules/{id}", true },
        { "delete", "/api/admin/schedules/{id}", true },
        { "post", "/api/admin/schedules/generate", true },
        { "post", "/api/admin/schedules/{id}/generate", true },
        { "get", "/api/admin/flights", true },
        { "post", "/api/admin/flights", true },
        { "get", "/api/admin/flights/{id}", true },
        { "put", "/api/admin/flights/{id}", true },
        { "delete", "/api/admin/flights/{id}", true },
        { "get", "/api/admin/flights/{id}/seats", true },
        { "patch", "/api/admin/flights/{id}/seats/{seatId}", true },
        { "get", "/api/admin/bookings", true },
        { "get", "/api/admin/bookings/{pnr}", true },
        { "post", "/api/admin/bookings/{pnr}/cancel", true },
        { "get", "/api/admin/passengers", true },
        { "get", "/api/admin/passengers/{documentType}/{documentNumber}", true },
        { "get", "/api/admin/users", true },
        { "post", "/api/admin/users", true },
        { "get", "/api/admin/users/{id}", true },
        { "put", "/api/admin/users/{id}/role", true },
        { "put", "/api/admin/users/{id}/active", true },
        { "delete", "/api/admin/users/{id}", true },
        { "get", "/api/admin/audit-logs", true },
        { "post", "/api/payments/checkout-session", false },
        { "get", "/api/payments/{paymentId}", false },
        { "post", "/api/payments/webhook", false }
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_IsPublishedWithTheExpectedProtection(string method, string path, bool needsToken)
    {
        using var document = JsonDocument.Parse(await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json"));

        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty(path, out var pathItem), $"{path} is missing");
        Assert.True(pathItem.TryGetProperty(method, out var operation), $"{method.ToUpperInvariant()} {path} is missing");
        Assert.Equal(needsToken, operation.TryGetProperty("security", out _));
    }

    [Fact]
    public async Task NoUnplannedEndpoints()
    {
        using var document = JsonDocument.Parse(await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json"));

        var published = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(op => $"{op.Name} {path.Name}"))
            .ToHashSet();
        var planned = Endpoints.Select(row => $"{row[0]} {row[1]}").ToHashSet();

        Assert.Empty(published.Except(planned));
    }
}
