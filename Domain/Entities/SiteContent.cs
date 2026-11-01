using Domain.Common;

namespace Domain.Entities;

/// <summary>One editable Azerbaijani text of a static site page (for example the "heroTitle" of the "about" page).</summary>
public class SiteContent : BaseEntity
{
    public string Page { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string TextAz { get; set; } = string.Empty;
}
