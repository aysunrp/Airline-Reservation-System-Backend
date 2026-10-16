using Domain.Enums;

namespace Service.Helpers.Settings;

/// <summary>
/// How seats are numbered when an aircraft is created from its cabin seat counts.
/// Cabins are laid out front to back (Business, Comfort, Economy); each row uses the cabin's seat letters
/// ('-' marks an aisle and is ignored), and an empty row number is left between cabins.
/// Defaults match the admin seat inventory page.
/// </summary>
public class SeatLayoutSettings
{
    public const string SectionName = "SeatLayout";

    public Dictionary<CabinClass, string> CabinLetters { get; set; } = new()
    {
        [CabinClass.Business] = "AB-CD",
        [CabinClass.Comfort] = "AB-C-DE",
        [CabinClass.Economy] = "ABC-DEF"
    };

    public int RowsBetweenCabins { get; set; } = 1;
}
