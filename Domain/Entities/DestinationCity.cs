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

    /// <summary>The city's own photograph: a file name in the frontend's assets/images folder.</summary>
    public string ImageFile { get; set; } = string.Empty;

    /// <summary>A short description of the city in the three site languages (the city page and its card show it).</summary>
    public string Description { get; set; } = string.Empty;
    public string DescriptionAz { get; set; } = string.Empty;
    public string DescriptionRu { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>AEROVA's home base (Baku): the default departure of the flight search. A hub is a place to fly from, so Explore Destinations does not offer it as a destination.</summary>
    public bool IsHub { get; set; }

    public Country Country { get; set; } = null!;
    public ICollection<Airport> Airports { get; set; } = new List<Airport>();
}
