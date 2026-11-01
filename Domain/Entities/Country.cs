using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// A country in the destination catalog. The two-letter code (ISO 3166-1 alpha-2) is its stable identity; the names and the short
/// description are kept in the three site languages so every page and the voice search read the same record.
/// </summary>
public class Country : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string DescriptionAz { get; set; } = string.Empty;
    public string DescriptionRu { get; set; } = string.Empty;

    /// <summary>File name of the local photograph in the frontend's assets/images folder (without a path), e.g. "azerbaijan-baku.jpg".</summary>
    public string ImageFile { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>A country that is switched off disappears from the catalog; nothing is deleted.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<DestinationCity> Cities { get; set; } = new List<DestinationCity>();
}
