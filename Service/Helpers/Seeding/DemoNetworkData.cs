using Domain.Enums;

namespace Service.Helpers.Seeding;

/// <summary>
/// The demonstration network added to the core one in <see cref="DemoSeedData"/>: routes from Baku to 45 destinations and back, and a few
/// connections between other hubs, with 104 schedules for November 2026 to January 2027. These are DEMONSTRATION timetables and fares,
/// not real airline schedules or prices. Distances are great-circle distances between the airports; the block time is the distance at
/// 720 km/h plus 35 minutes; local times and the arrival day follow from the airports' time zones; the Economy fare is 45 + 0.088 per km,
/// Comfort is 1.38 times and Business 2.25 times that. The operating days and departure times differ by route (no route flies every day
/// at the same hour). Each schedule ends on 31 January 2027 so the background worker never extends the demo network past January 2027.
/// </summary>
public static class DemoNetworkData
{
    public static readonly DateOnly ValidTo = new(2027, 1, 31);

    private const ScheduleDays Mon = ScheduleDays.Monday;
    private const ScheduleDays Tue = ScheduleDays.Tuesday;
    private const ScheduleDays Wed = ScheduleDays.Wednesday;
    private const ScheduleDays Thu = ScheduleDays.Thursday;
    private const ScheduleDays Fri = ScheduleDays.Friday;
    private const ScheduleDays Sat = ScheduleDays.Saturday;
    private const ScheduleDays Sun = ScheduleDays.Sunday;

    // Departure and arrival are local times at the origin and the destination; the offset is the number of days between the two dates.
    private static ScheduleSeed S(string flightNumber, string origin, string destination, string aircraft, int depHour, int depMinute,
        int arrHour, int arrMinute, int arrivalDayOffset, decimal economy, decimal comfort, decimal business, ScheduleDays days)
        => new(flightNumber, origin, destination, aircraft, new TimeOnly(depHour, depMinute), new TimeOnly(arrHour, arrMinute),
            arrivalDayOffset, economy, comfort, business, null, days, ValidTo);

    /// <summary>Same layout (12 Business, 25 Comfort, 60 Economy) as the core aircraft, so the seat map is the same on every flight.</summary>
    public static readonly IReadOnlyList<AircraftSeed> Aircraft = new[]
    {
        new AircraftSeed("AEROVA A320neo 2", "A320neo", "4K-AV05", 12, 25, 60),
        new AircraftSeed("AEROVA A320neo 3", "A320neo", "4K-AV06", 12, 25, 60),
        new AircraftSeed("AEROVA A320neo 4", "A320neo", "4K-AV07", 12, 25, 60),
        new AircraftSeed("AEROVA A321neo 2", "A321neo", "4K-AV08", 12, 25, 60),
        new AircraftSeed("AEROVA A321neo 3", "A321neo", "4K-AV09", 12, 25, 60),
        new AircraftSeed("AEROVA A350-900 2", "A350-900", "4K-AV10", 12, 25, 60),
        new AircraftSeed("AEROVA 787-9 2", "787-9", "4K-AV11", 12, 25, 60),
        new AircraftSeed("AEROVA 787-9 3", "787-9", "4K-AV12", 12, 25, 60)
    };

    public static readonly IReadOnlyList<RouteSeed> Routes = new[]
    {
        new RouteSeed("GYD", "GNJ", 316, 60),
        new RouteSeed("GNJ", "GYD", 316, 60),
        new RouteSeed("GYD", "TBS", 447, 70),
        new RouteSeed("TBS", "GYD", 447, 70),
        new RouteSeed("GYD", "BUS", 719, 95),
        new RouteSeed("BUS", "GYD", 719, 95),
        new RouteSeed("GYD", "KUT", 659, 90),
        new RouteSeed("KUT", "GYD", 659, 90),
        new RouteSeed("GYD", "ESB", 1444, 155),
        new RouteSeed("ESB", "GYD", 1444, 155),
        new RouteSeed("GYD", "AYT", 1713, 180),
        new RouteSeed("AYT", "GYD", 1713, 180),
        new RouteSeed("GYD", "ADB", 1976, 200),
        new RouteSeed("ADB", "GYD", 1976, 200),
        new RouteSeed("GYD", "TZX", 865, 105),
        new RouteSeed("TZX", "GYD", 865, 105),
        new RouteSeed("GYD", "AUH", 1833, 190),
        new RouteSeed("AUH", "GYD", 1833, 190),
        new RouteSeed("GYD", "SHJ", 1758, 180),
        new RouteSeed("SHJ", "GYD", 1758, 180),
        new RouteSeed("GYD", "FRA", 3367, 315),
        new RouteSeed("FRA", "GYD", 3367, 315),
        new RouteSeed("GYD", "MUC", 3127, 295),
        new RouteSeed("MUC", "GYD", 3127, 295),
        new RouteSeed("GYD", "BER", 3055, 290),
        new RouteSeed("BER", "GYD", 3055, 290),
        new RouteSeed("GYD", "HAM", 3312, 310),
        new RouteSeed("HAM", "GYD", 3312, 310),
        new RouteSeed("GYD", "NCE", 3514, 330),
        new RouteSeed("NCE", "GYD", 3514, 330),
        new RouteSeed("GYD", "LYS", 3647, 340),
        new RouteSeed("LYS", "GYD", 3647, 340),
        new RouteSeed("GYD", "MRS", 3677, 340),
        new RouteSeed("MRS", "GYD", 3677, 340),
        new RouteSeed("GYD", "MAN", 4118, 380),
        new RouteSeed("MAN", "GYD", 4118, 380),
        new RouteSeed("GYD", "BHX", 4085, 375),
        new RouteSeed("BHX", "GYD", 4085, 375),
        new RouteSeed("GYD", "EDI", 4188, 385),
        new RouteSeed("EDI", "GYD", 4188, 385),
        new RouteSeed("GYD", "FCO", 3144, 295),
        new RouteSeed("FCO", "GYD", 3144, 295),
        new RouteSeed("GYD", "MXP", 3368, 315),
        new RouteSeed("MXP", "GYD", 3368, 315),
        new RouteSeed("GYD", "VCE", 3087, 290),
        new RouteSeed("VCE", "GYD", 3087, 290),
        new RouteSeed("GYD", "NAP", 2994, 285),
        new RouteSeed("NAP", "GYD", 2994, 285),
        new RouteSeed("GYD", "MAD", 4461, 405),
        new RouteSeed("MAD", "GYD", 4461, 405),
        new RouteSeed("GYD", "BCN", 3981, 365),
        new RouteSeed("BCN", "GYD", 3981, 365),
        new RouteSeed("GYD", "AGP", 4685, 425),
        new RouteSeed("AGP", "GYD", 4685, 425),
        new RouteSeed("GYD", "WAW", 2559, 250),
        new RouteSeed("WAW", "GYD", 2559, 250),
        new RouteSeed("GYD", "KRK", 2574, 250),
        new RouteSeed("KRK", "GYD", 2574, 250),
        new RouteSeed("GYD", "AMS", 3643, 340),
        new RouteSeed("AMS", "GYD", 3643, 340),
        new RouteSeed("GYD", "VIE", 2771, 265),
        new RouteSeed("VIE", "GYD", 2771, 265),
        new RouteSeed("GYD", "SZG", 3034, 290),
        new RouteSeed("SZG", "GYD", 3034, 290),
        new RouteSeed("GYD", "ALA", 2246, 220),
        new RouteSeed("ALA", "GYD", 2246, 220),
        new RouteSeed("GYD", "NQZ", 2022, 205),
        new RouteSeed("NQZ", "GYD", 2022, 205),
        new RouteSeed("GYD", "CIT", 1630, 170),
        new RouteSeed("CIT", "GYD", 1630, 170),
        new RouteSeed("GYD", "SCO", 386, 65),
        new RouteSeed("SCO", "GYD", 386, 65),
        new RouteSeed("GYD", "GUW", 753, 100),
        new RouteSeed("GUW", "GYD", 753, 100),
        new RouteSeed("GYD", "TAS", 1616, 170),
        new RouteSeed("TAS", "GYD", 1616, 170),
        new RouteSeed("GYD", "SKD", 1441, 155),
        new RouteSeed("SKD", "GYD", 1441, 155),
        new RouteSeed("GYD", "BHK", 1228, 135),
        new RouteSeed("BHK", "GYD", 1228, 135),
        new RouteSeed("GYD", "UGC", 896, 110),
        new RouteSeed("UGC", "GYD", 896, 110),
        new RouteSeed("GYD", "HND", 7522, 660),
        new RouteSeed("HND", "GYD", 7522, 660),
        new RouteSeed("GYD", "KIX", 7249, 640),
        new RouteSeed("KIX", "GYD", 7249, 640),
        new RouteSeed("GYD", "NGO", 7342, 645),
        new RouteSeed("NGO", "GYD", 7342, 645),
        new RouteSeed("GYD", "MLE", 4668, 425),
        new RouteSeed("MLE", "GYD", 4668, 425),
        new RouteSeed("IST", "LHR", 2488, 240),
        new RouteSeed("LHR", "IST", 2488, 240),
        new RouteSeed("IST", "FRA", 1837, 190),
        new RouteSeed("FRA", "IST", 1837, 190),
        new RouteSeed("IST", "CDG", 2213, 220),
        new RouteSeed("CDG", "IST", 2213, 220),
        new RouteSeed("IST", "DXB", 3028, 285),
        new RouteSeed("DXB", "IST", 3028, 285),
        new RouteSeed("DXB", "MLE", 3040, 290),
        new RouteSeed("MLE", "DXB", 3040, 290),
        new RouteSeed("IST", "TAS", 3355, 315),
        new RouteSeed("TAS", "IST", 3355, 315)
    };

    public static readonly IReadOnlyList<ScheduleSeed> Schedules = new[]
    {
        S("AV 1001", "GYD", "GNJ", "4K-AV04", 20, 25, 21, 25, 0, 75, 105, 170, Mon | Tue | Wed | Thu | Fri | Sat | Sun),
        S("AV 1002", "GNJ", "GYD", "4K-AV05", 8, 15, 9, 15, 0, 75, 105, 170, Mon | Tue | Wed | Thu | Fri | Sat | Sun),
        S("AV 1003", "GYD", "TBS", "4K-AV06", 10, 55, 12, 5, 0, 85, 115, 190, Mon | Tue | Wed | Thu | Fri | Sat | Sun),
        S("AV 1004", "GYD", "TBS", "4K-AV07", 20, 25, 21, 35, 0, 85, 115, 190, Mon | Fri),
        S("AV 1005", "TBS", "GYD", "4K-AV04", 12, 55, 14, 5, 0, 85, 115, 190, Mon | Tue | Wed | Thu | Fri | Sat | Sun),
        S("AV 1006", "TBS", "GYD", "4K-AV05", 19, 45, 20, 55, 0, 85, 115, 190, Mon | Fri),
        S("AV 1007", "GYD", "BUS", "4K-AV06", 16, 20, 17, 55, 0, 110, 150, 250, Mon | Wed | Fri),
        S("AV 1008", "BUS", "GYD", "4K-AV07", 17, 35, 19, 10, 0, 110, 150, 250, Tue | Thu | Sat),
        S("AV 1009", "GYD", "KUT", "4K-AV04", 8, 30, 10, 0, 0, 105, 145, 235, Tue | Fri),
        S("AV 1010", "KUT", "GYD", "4K-AV05", 11, 35, 13, 5, 0, 105, 145, 235, Wed | Sat),
        S("AV 1011", "GYD", "ESB", "4K-AV06", 14, 50, 16, 25, 0, 170, 235, 380, Mon | Wed | Fri),
        S("AV 1012", "ESB", "GYD", "4K-AV07", 20, 25, 0, 0, 1, 170, 235, 380, Tue | Thu | Sat),
        S("AV 1013", "GYD", "AYT", "4K-AV04", 16, 5, 18, 5, 0, 195, 270, 440, Mon | Wed | Fri),
        S("AV 1014", "AYT", "GYD", "4K-AV05", 18, 15, 22, 15, 0, 195, 270, 440, Tue | Thu | Sat),
        S("AV 1015", "GYD", "ADB", "4K-AV06", 20, 35, 22, 55, 0, 220, 305, 495, Tue | Fri),
        S("AV 1016", "ADB", "GYD", "4K-AV07", 7, 25, 11, 45, 0, 220, 305, 495, Wed | Sat),
        S("AV 1017", "GYD", "TZX", "4K-AV04", 12, 40, 13, 25, 0, 120, 165, 270, Tue | Fri),
        S("AV 1018", "TZX", "GYD", "4K-AV05", 17, 40, 20, 25, 0, 120, 165, 270, Wed | Sat),
        S("AV 1019", "GYD", "AUH", "4K-AV06", 14, 55, 18, 5, 0, 205, 285, 460, Mon | Tue | Thu | Sat),
        S("AV 1020", "AUH", "GYD", "4K-AV07", 20, 55, 0, 5, 1, 205, 285, 460, Tue | Wed | Fri | Sun),
        S("AV 1021", "GYD", "SHJ", "4K-AV04", 11, 55, 14, 55, 0, 200, 275, 450, Mon | Wed | Fri),
        S("AV 1022", "SHJ", "GYD", "4K-AV05", 15, 35, 18, 35, 0, 200, 275, 450, Tue | Thu | Sat),
        S("AV 1023", "GYD", "FRA", "4K-AV03", 8, 20, 10, 35, 0, 340, 470, 765, Mon | Tue | Thu | Sat),
        S("AV 1024", "FRA", "GYD", "4K-AV08", 10, 15, 18, 30, 0, 340, 470, 765, Tue | Wed | Fri | Sun),
        S("AV 1025", "GYD", "MUC", "4K-AV09", 16, 35, 18, 30, 0, 320, 440, 720, Mon | Wed | Fri),
        S("AV 1026", "MUC", "GYD", "4K-AV03", 20, 5, 4, 0, 1, 320, 440, 720, Tue | Thu | Sat),
        S("AV 1027", "GYD", "BER", "4K-AV08", 7, 10, 9, 0, 0, 315, 435, 710, Mon | Wed | Fri),
        S("AV 1028", "BER", "GYD", "4K-AV09", 11, 15, 19, 5, 0, 315, 435, 710, Tue | Thu | Sat),
        S("AV 1029", "GYD", "HAM", "4K-AV03", 12, 25, 14, 35, 0, 335, 460, 755, Tue | Fri),
        S("AV 1030", "HAM", "GYD", "4K-AV08", 19, 35, 3, 45, 1, 335, 460, 755, Wed | Sat),
        S("AV 1031", "GYD", "NCE", "4K-AV09", 14, 0, 16, 30, 0, 355, 490, 800, Mon | Wed | Fri),
        S("AV 1032", "NCE", "GYD", "4K-AV03", 19, 20, 3, 50, 1, 355, 490, 800, Tue | Thu | Sat),
        S("AV 1033", "GYD", "LYS", "4K-AV08", 11, 10, 13, 50, 0, 365, 505, 820, Tue | Fri),
        S("AV 1034", "LYS", "GYD", "4K-AV09", 16, 5, 0, 45, 1, 365, 505, 820, Wed | Sat),
        S("AV 1035", "GYD", "MRS", "4K-AV03", 8, 15, 10, 55, 0, 370, 510, 830, Tue | Fri),
        S("AV 1036", "MRS", "GYD", "4K-AV08", 9, 30, 18, 10, 0, 370, 510, 830, Wed | Sat),
        S("AV 1037", "GYD", "MAN", "4K-AV09", 20, 30, 22, 50, 0, 405, 560, 910, Mon | Wed | Fri),
        S("AV 1038", "MAN", "GYD", "4K-AV03", 22, 15, 8, 35, 1, 405, 560, 910, Tue | Thu | Sat),
        S("AV 1039", "GYD", "BHX", "4K-AV08", 23, 20, 1, 35, 1, 405, 560, 910, Tue | Fri),
        S("AV 1040", "BHX", "GYD", "4K-AV09", 7, 50, 18, 5, 0, 405, 560, 910, Wed | Sat),
        S("AV 1041", "GYD", "EDI", "4K-AV03", 19, 20, 21, 45, 0, 415, 575, 935, Tue | Fri),
        S("AV 1042", "EDI", "GYD", "4K-AV08", 22, 25, 8, 50, 1, 415, 575, 935, Wed | Sat),
        S("AV 1043", "GYD", "FCO", "4K-AV09", 22, 35, 0, 30, 1, 320, 440, 720, Mon | Tue | Thu | Sat),
        S("AV 1044", "FCO", "GYD", "4K-AV03", 7, 20, 15, 15, 0, 320, 440, 720, Tue | Wed | Fri | Sun),
        S("AV 1045", "GYD", "MXP", "4K-AV08", 17, 10, 19, 25, 0, 340, 470, 765, Mon | Wed | Fri),
        S("AV 1046", "MXP", "GYD", "4K-AV09", 21, 50, 6, 5, 1, 340, 470, 765, Tue | Thu | Sat),
        S("AV 1047", "GYD", "VCE", "4K-AV03", 8, 15, 10, 5, 0, 315, 435, 710, Tue | Fri),
        S("AV 1048", "VCE", "GYD", "4K-AV08", 11, 40, 19, 30, 0, 315, 435, 710, Wed | Sat),
        S("AV 1049", "GYD", "NAP", "4K-AV09", 9, 30, 11, 15, 0, 310, 430, 700, Tue | Fri),
        S("AV 1050", "NAP", "GYD", "4K-AV03", 16, 25, 0, 10, 1, 310, 430, 700, Wed | Sat),
        S("AV 1051", "GYD", "MAD", "4K-AV08", 17, 0, 20, 45, 0, 440, 605, 990, Mon | Wed | Fri),
        S("AV 1052", "MAD", "GYD", "4K-AV09", 0, 55, 10, 40, 0, 440, 605, 990, Tue | Thu | Sat),
        S("AV 1053", "GYD", "BCN", "4K-AV03", 23, 5, 2, 10, 1, 395, 545, 890, Mon | Wed | Fri),
        S("AV 1054", "BCN", "GYD", "4K-AV08", 6, 20, 15, 25, 0, 395, 545, 890, Tue | Thu | Sat),
        S("AV 1055", "GYD", "AGP", "4K-AV09", 0, 20, 4, 25, 0, 455, 630, 1025, Tue | Fri),
        S("AV 1056", "AGP", "GYD", "4K-AV03", 7, 15, 17, 20, 0, 455, 630, 1025, Wed | Sat),
        S("AV 1057", "GYD", "WAW", "4K-AV08", 15, 45, 16, 55, 0, 270, 375, 610, Mon | Wed | Fri),
        S("AV 1058", "WAW", "GYD", "4K-AV09", 17, 10, 0, 20, 1, 270, 375, 610, Tue | Thu | Sat),
        S("AV 1059", "GYD", "KRK", "4K-AV03", 8, 0, 9, 10, 0, 270, 375, 610, Tue | Fri),
        S("AV 1060", "KRK", "GYD", "4K-AV08", 11, 0, 18, 10, 0, 270, 375, 610, Wed | Sat),
        S("AV 1061", "GYD", "AMS", "4K-AV09", 20, 45, 23, 25, 0, 365, 505, 820, Mon | Wed | Fri),
        S("AV 1062", "AMS", "GYD", "4K-AV03", 0, 10, 8, 50, 0, 365, 505, 820, Tue | Thu | Sat),
        S("AV 1063", "GYD", "VIE", "4K-AV08", 9, 20, 10, 45, 0, 290, 400, 650, Mon | Wed | Fri),
        S("AV 1064", "VIE", "GYD", "4K-AV09", 14, 5, 21, 30, 0, 290, 400, 650, Tue | Thu | Sat),
        S("AV 1065", "GYD", "SZG", "4K-AV03", 16, 25, 18, 15, 0, 310, 430, 700, Tue | Fri),
        S("AV 1066", "SZG", "GYD", "4K-AV08", 19, 20, 3, 10, 1, 310, 430, 700, Wed | Sat),
        S("AV 1067", "GYD", "ALA", "4K-AV06", 15, 40, 20, 20, 0, 245, 340, 550, Mon | Tue | Thu | Sat),
        S("AV 1068", "ALA", "GYD", "4K-AV07", 19, 5, 21, 45, 0, 245, 340, 550, Tue | Wed | Fri | Sun),
        S("AV 1069", "GYD", "NQZ", "4K-AV04", 9, 50, 14, 15, 0, 225, 310, 505, Mon | Wed | Fri),
        S("AV 1070", "NQZ", "GYD", "4K-AV05", 14, 45, 17, 10, 0, 225, 310, 505, Tue | Thu | Sat),
        S("AV 1071", "GYD", "CIT", "4K-AV06", 8, 25, 12, 15, 0, 190, 260, 430, Tue | Fri),
        S("AV 1072", "CIT", "GYD", "4K-AV07", 10, 40, 12, 30, 0, 190, 260, 430, Wed | Sat),
        S("AV 1073", "GYD", "SCO", "4K-AV04", 9, 15, 11, 20, 0, 80, 110, 180, Tue | Fri),
        S("AV 1074", "SCO", "GYD", "4K-AV05", 16, 15, 16, 20, 0, 80, 110, 180, Wed | Sat),
        S("AV 1075", "GYD", "GUW", "4K-AV06", 18, 25, 21, 5, 0, 110, 150, 250, Tue | Fri),
        S("AV 1076", "GUW", "GYD", "4K-AV07", 6, 10, 6, 50, 0, 110, 150, 250, Wed | Sat),
        S("AV 1077", "GYD", "TAS", "4K-AV04", 6, 10, 10, 0, 0, 185, 255, 415, Mon | Tue | Wed | Thu | Fri),
        S("AV 1078", "TAS", "GYD", "4K-AV05", 10, 35, 12, 25, 0, 185, 255, 415, Tue | Wed | Thu | Fri | Sat),
        S("AV 1079", "GYD", "SKD", "4K-AV06", 16, 30, 20, 5, 0, 170, 235, 380, Mon | Wed | Fri),
        S("AV 1080", "SKD", "GYD", "4K-AV07", 20, 40, 22, 15, 0, 170, 235, 380, Tue | Thu | Sat),
        S("AV 1081", "GYD", "BHK", "4K-AV04", 11, 55, 15, 10, 0, 155, 215, 350, Tue | Fri),
        S("AV 1082", "BHK", "GYD", "4K-AV05", 16, 15, 17, 30, 0, 155, 215, 350, Wed | Sat),
        S("AV 1083", "GYD", "UGC", "4K-AV06", 18, 55, 21, 45, 0, 125, 170, 280, Tue | Fri),
        S("AV 1084", "UGC", "GYD", "4K-AV07", 6, 20, 7, 10, 0, 125, 170, 280, Wed | Sat),
        S("AV 1085", "GYD", "HND", "4K-AV02", 21, 20, 13, 20, 1, 705, 975, 1585, Mon | Wed | Fri),
        S("AV 1086", "HND", "GYD", "4K-AV01", 10, 35, 16, 35, 0, 705, 975, 1585, Tue | Thu | Sat),
        S("AV 1087", "GYD", "KIX", "4K-AV10", 18, 5, 9, 45, 1, 685, 945, 1540, Tue | Fri),
        S("AV 1088", "KIX", "GYD", "4K-AV11", 0, 45, 6, 25, 0, 685, 945, 1540, Wed | Sat),
        S("AV 1089", "GYD", "NGO", "4K-AV12", 20, 5, 11, 50, 1, 690, 950, 1550, Tue | Fri),
        S("AV 1090", "NGO", "GYD", "4K-AV02", 21, 20, 3, 5, 1, 690, 950, 1550, Wed | Sat),
        S("AV 1091", "GYD", "MLE", "4K-AV09", 7, 30, 15, 35, 0, 455, 630, 1025, Mon | Wed | Fri),
        S("AV 1092", "MLE", "GYD", "4K-AV03", 10, 45, 16, 50, 0, 455, 630, 1025, Tue | Thu | Sat),
        S("AV 1093", "IST", "LHR", "4K-AV04", 13, 40, 14, 40, 0, 265, 365, 595, Tue | Fri),
        S("AV 1094", "LHR", "IST", "4K-AV05", 17, 55, 0, 55, 1, 265, 365, 595, Wed | Sat),
        S("AV 1095", "IST", "FRA", "4K-AV06", 10, 5, 11, 15, 0, 205, 285, 460, Tue | Fri),
        S("AV 1096", "FRA", "IST", "4K-AV07", 14, 5, 19, 15, 0, 205, 285, 460, Wed | Sat),
        S("AV 1097", "IST", "CDG", "4K-AV04", 13, 55, 15, 35, 0, 240, 330, 540, Tue | Fri),
        S("AV 1098", "CDG", "IST", "4K-AV05", 17, 50, 23, 30, 0, 240, 330, 540, Wed | Sat),
        S("AV 1099", "IST", "DXB", "4K-AV08", 13, 45, 19, 30, 0, 310, 430, 700, Tue | Fri),
        S("AV 1100", "DXB", "IST", "4K-AV09", 18, 50, 22, 35, 0, 310, 430, 700, Wed | Sat),
        S("AV 1101", "DXB", "MLE", "4K-AV03", 23, 40, 5, 30, 1, 315, 435, 710, Mon | Wed | Fri),
        S("AV 1102", "MLE", "DXB", "4K-AV08", 7, 10, 11, 0, 0, 315, 435, 710, Tue | Thu | Sat),
        S("AV 1103", "IST", "TAS", "4K-AV09", 16, 5, 23, 20, 0, 340, 470, 765, Tue | Fri),
        S("AV 1104", "TAS", "IST", "4K-AV03", 17, 0, 20, 15, 0, 340, 470, 765, Wed | Sat)
    };
}
