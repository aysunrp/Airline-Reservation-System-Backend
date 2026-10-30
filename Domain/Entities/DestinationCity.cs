using Domain.Common;

namespace Domain.Entities;

/// <summary>A city of a <see cref="Country"/> in the destination catalog; its airports point back to it through <see cref="Airport.CityId"/>.</summary>
public class DestinationCity : BaseEntity
{
    public int CountryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;

    /// <summary>Other spellings people say or type (transliterations, local names), separated by semicolons. The voice search reads them.</summary>
    public string? Aliases { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Country Country { get; set; } = null!;
    public ICollection<Airport> Airports { get; set; } = new List<Airport>();
}
