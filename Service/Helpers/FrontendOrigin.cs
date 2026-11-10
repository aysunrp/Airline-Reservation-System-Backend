namespace Service.Helpers;

/// <summary>
/// Stripe success/cancel URLs must land on the same browser origin where checkout started,
/// otherwise localStorage and sessionStorage (JWT, payer snapshot) are empty or from another profile.
/// </summary>
public static class FrontendOrigin
{
    public static string ResolveBaseUrl(string? returnOrigin, string configuredFrontendBaseUrl)
    {
        var fallback = configuredFrontendBaseUrl.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(returnOrigin))
        {
            return fallback;
        }

        var candidate = returnOrigin.Trim().TrimEnd('/');
        return IsAllowed(candidate, fallback) ? candidate : fallback;
    }

    public static bool IsAllowed(string candidate, string configuredBase)
    {
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var origin))
        {
            return false;
        }

        if (origin.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (!Uri.TryCreate(configuredBase, UriKind.Absolute, out var configured))
        {
            return false;
        }

        if (!string.Equals(origin.Scheme, configured.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (origin.Port != configured.Port)
        {
            return false;
        }

        if (string.Equals(origin.Host, configured.Host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Local dev: booking on 127.0.0.1 vs configured localhost (same port) must still return to the tab that paid.
        var devHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "localhost", "127.0.0.1" };
        return devHosts.Contains(origin.Host) && devHosts.Contains(configured.Host);
    }
}
