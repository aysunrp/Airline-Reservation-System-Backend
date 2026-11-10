using Domain.Common;

namespace Domain.Entities;

/// <summary>One question and answer of the support page's FAQ, managed in the admin panel.</summary>
public class FaqItem : BaseEntity
{
    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
