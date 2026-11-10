using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// A card of the home page: "why" cards (the AEROVA features) and "premium" cards (the cabin classes), each with its text, its link,
/// its order and whether it is shown. A premium card has a photograph: <see cref="ImagePath"/> is either a path inside Media/ (an
/// uploaded file, for example home/img-....webp) or a path of the site itself (assets/images/...).
/// </summary>
public class HomeCard : BaseEntity
{
    /// <summary>"why" or "premium".</summary>
    public string Section { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>A small label above the title (premium cards).</summary>
    public string? Label { get; set; }

    public string? LinkText { get; set; }

    /// <summary>A page of the site (for example booking.html or about-aerova.html#economy) or an https address.</summary>
    public string? LinkUrl { get; set; }

    /// <summary>The icon of a "why" card: booking, seat, trips, status, premium or support.</summary>
    public string? IconKey { get; set; }

    public string? ImagePath { get; set; }

    public string? ImageAlt { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
