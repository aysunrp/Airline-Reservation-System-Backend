using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Tests;

/// <summary>Replaces Stripe's refunds in the API tests: nothing is sent, the requests are kept and the answer is whatever the test sets.</summary>
public sealed class TestStripeRefundGateway : IStripeRefundGateway
{
    private readonly object _lock = new();
    private readonly List<StripeRefundRequest> _requests = new();

    public bool Unavailable { get; set; }

    public Func<StripeRefundRequest, StripeRefundResult> Answer { get; set; } = Succeeded;

    public static StripeRefundResult Succeeded(StripeRefundRequest request) => new($"re_test_{request.RefundId}_{request.Attempt}", "succeeded", null);

    public IReadOnlyList<StripeRefundRequest> Requests
    {
        get
        {
            lock (_lock)
            {
                return _requests.ToList();
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _requests.Clear();
        }

        Unavailable = false;
        Answer = Succeeded;
    }

    public Task<StripeRefundResult> CreateRefundAsync(StripeRefundRequest request, CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            throw new ServiceUnavailableException("Refunds are not available right now. Please try again later.");
        }

        lock (_lock)
        {
            _requests.Add(request);
        }

        return Task.FromResult(Answer(request));
    }
}
