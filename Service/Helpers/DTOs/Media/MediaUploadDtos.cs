namespace Service.Helpers.DTOs.Media;

/// <summary>Relative path is stored in the database; publicUrl is for admin preview.</summary>
public record MediaUploadResultDto(string RelativePath, string PublicUrl);

public record DestinationImageDto(int Id, string ImageFile, string? PublicUrl);
