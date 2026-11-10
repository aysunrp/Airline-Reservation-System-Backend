namespace Service.Helpers;

/// <summary>How travel document numbers are compared: "ab 123-456" and "AB123456" are the same number.</summary>
public static class DocumentNumbers
{
    /// <summary>Upper case, without spaces, dashes or other separators.</summary>
    public static string Normalize(string? number)
        => new((number ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
