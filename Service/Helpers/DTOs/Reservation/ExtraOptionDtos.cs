namespace Service.Helpers.DTOs.Reservation;

/// <summary>An extra checked-baggage choice on the baggage page ("No extra baggage" is simply no option).</summary>
public record BaggageOptionDto(int Id, string Code, string Name, int WeightKg, decimal Price, bool IsActive);

/// <summary>A meal choice on the meal page ("No meal" is simply no option).</summary>
public record MealOptionDto(int Id, string Code, string Name, string? Description, decimal Price, bool IsActive);

public class BaggageOptionRequestDto
{
    /// <summary>Short key such as bag20; stored in lower case and unique.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int WeightKg { get; set; }
    public decimal Price { get; set; }

    /// <summary>Inactive options are no longer offered; tickets that already have them keep them.</summary>
    public bool IsActive { get; set; } = true;
}

public class MealOptionRequestDto
{
    /// <summary>Short key such as classic; stored in lower case and unique.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }

    /// <summary>Inactive options are no longer offered; tickets that already have them keep them.</summary>
    public bool IsActive { get; set; } = true;
}
