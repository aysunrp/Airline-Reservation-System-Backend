namespace Service.Helpers.DTOs.Cms;

public record FaqItemDto(int Id, string Question, string Answer, int SortOrder, bool IsActive);

public class FaqItemRequestDto
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;

    /// <summary>The position in the list; empty puts the new question at the end.</summary>
    public int? SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A home page card. <see cref="ImagePath"/> is what is stored (a path inside Media/ or a path of the site itself);
/// <see cref="ImageUrl"/> is the address the page can load it from.
/// </summary>
public record HomeCardDto(
    int Id, string Section, string Title, string Description, string? Label, string? LinkText, string? LinkUrl, string? IconKey,
    string? ImagePath, string? ImageUrl, string? ImageAlt, int SortOrder, bool IsActive);

/// <summary>
/// The image is not part of this request: it is uploaded with its own call, so only a real uploaded file can become the image of a card.
/// </summary>
public class HomeCardRequestDto
{
    public string Section { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? LinkText { get; set; }
    public string? LinkUrl { get; set; }
    public string? IconKey { get; set; }
    public string? ImageAlt { get; set; }
    public int? SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public record CardImageDto(string ImagePath, string ImageUrl);
