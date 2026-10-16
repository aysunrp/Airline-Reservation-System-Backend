using System.Text.RegularExpressions;

namespace Service.Helpers.Validators;

/// <summary>Flight numbers as the frontend shows them: two-letter airline code, a space and 1–4 digits ("AV 101").</summary>
public static class FlightNumber
{
    private static readonly Regex Pattern = new(@"^\s*([A-Za-z]{2})\s*(\d{1,4})\s*$");

    public static bool IsValid(string? value) => value is not null && Pattern.IsMatch(value);

    /// <summary>"av101", " AV  101 " → "AV 101".</summary>
    public static string Normalize(string value)
    {
        var match = Pattern.Match(value);
        return match.Success ? $"{match.Groups[1].Value.ToUpperInvariant()} {match.Groups[2].Value}" : value.Trim();
    }
}
