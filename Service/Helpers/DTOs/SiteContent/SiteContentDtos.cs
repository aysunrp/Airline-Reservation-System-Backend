namespace Service.Helpers.DTOs.SiteContent;

/// <summary>One Azerbaijani text of a page.</summary>
public record SiteContentDto(int Id, string Page, string Key, string TextAz);

/// <summary>An admin edit of one text. The page and the key stay as they are: the page looks its texts up by key.</summary>
public class SiteContentRequestDto
{
    /// <summary>Nullable so that a missing text reaches the validator and is answered in Azerbaijani.</summary>
    public string? TextAz { get; set; }
}

public record SiteContentSeedResultDto(int Added, int AlreadyPresent);
