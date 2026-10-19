namespace Domain.Constants;

/// <summary>Titles offered on the profile and passenger forms.</summary>
public static class PersonTitles
{
    public static readonly IReadOnlyList<string> All = new[] { "Mr", "Ms", "Mrs", "Mx" };

    public static bool IsValid(string? title) => title is not null && All.Contains(title);

    public static string Description => string.Join(", ", All.Take(All.Count - 1)) + " or " + All[^1];
}
