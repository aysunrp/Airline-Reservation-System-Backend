using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Tests;

/// <summary>Replaces Stripe in the API tests: nothing is sent, the requested Checkout pages are kept for the test to read.</summary>
public sealed class TestStripeGateway : IStripeCheckoutGateway
{
    private readonly object _lock = new();
    private readonly List<StripeCheckoutRequest> _created = new();

    public bool Fail { get; set; }

    public IReadOnlyList<StripeCheckoutRequest> Created
    {
        get
        {
            lock (_lock)
            {
                return _created.ToList();
            }
        }
    }

    public Task<StripeCheckoutSession> CreateSessionAsync(StripeCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new ServiceUnavailableException("Card payments are not available right now. Please try again later.");
        }

        lock (_lock)
        {
            _created.Add(request);
        }

        var id = $"cs_test_{Guid.NewGuid():N}";
        return Task.FromResult(new StripeCheckoutSession(id, $"https://checkout.stripe.test/c/pay/{id}", request.ExpiresAtUtc));
    }

    public Task ExpireSessionAsync(string sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
