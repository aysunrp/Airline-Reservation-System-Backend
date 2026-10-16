namespace Service.Helpers.DTOs.Reservation;

/// <summary>An extra checked-baggage choice on the baggage page ("No extra baggage" is simply no option).</summary>
public record BaggageOptionDto(int Id, string Code, string Name, int WeightKg, decimal Price);

/// <summary>A meal choice on the meal page ("No meal" is simply no option).</summary>
public record MealOptionDto(int Id, string Code, string Name, string? Description, decimal Price);
