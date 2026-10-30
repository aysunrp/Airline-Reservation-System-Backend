namespace Service.Helpers.Seeding;

public sealed record CatalogAirportSeed(string Code, string Name, string TimeZoneId);

public sealed record CatalogCitySeed(string Name, string NameAz, string NameRu, string[] Aliases, CatalogAirportSeed[] Airports)
{
    /// <summary>AEROVA's home base: kept in the catalog (search origin, airports, routes) but not shown as a place to travel to.</summary>
    public bool IsHub { get; init; }
}

public sealed record CatalogCountrySeed(
    string Code, string Name, string NameAz, string NameRu,
    string Description, string DescriptionAz, string DescriptionRu,
    string ImageFile, CatalogCitySeed[] Cities);

/// <summary>
/// The destination catalog: 20 countries, their cities and the airports that serve them. It is reference data, not demo data: it says
/// where AEROVA may fly, never that a flight exists. Routes, schedules and fares are added separately (the demo network or the admin panel).
/// Every IATA code was checked against the airport's Wikipedia article. The image files are in the frontend's assets/images folder
/// (sources and licences: docs/destination-images.md).
/// </summary>
public static class DestinationCatalogData
{
    private static CatalogAirportSeed A(string code, string name, string zone) => new(code, name, zone);

    private static CatalogCitySeed C(string name, string az, string ru, string[] aliases, params CatalogAirportSeed[] airports) => new(name, az, ru, aliases, airports);

    private static CatalogCitySeed H(string name, string az, string ru, string[] aliases, params CatalogAirportSeed[] airports) => C(name, az, ru, aliases, airports) with { IsHub = true };

    private static readonly string[] None = Array.Empty<string>();

    public static readonly IReadOnlyList<CatalogCountrySeed> Countries = new[]
    {
        new CatalogCountrySeed("AZ", "Azerbaijan", "Azərbaycan", "Азербайджан",
            "Baku's Flame Towers glow above the Caspian waterfront and the walled Old City.",
            "Bakının Alov Qüllələri Xəzər sahilində və İçərişəhərin üstündə parlayır.",
            "Огненные башни Баку светятся над набережной Каспия и старым городом.",
            "azerbaijan-baku.jpg", new[]
            {
                H("Baku", "Bakı", "Баку", new[] { "Baki", "Baku" }, A("GYD", "Heydar Aliyev International Airport", "Asia/Baku")),
                C("Ganja", "Gəncə", "Гянджа", new[] { "Gence", "Gandja", "Гянджа", "Гянджи", "Гянджу" }, A("GNJ", "Ganja International Airport", "Asia/Baku"))
            }),
        new CatalogCountrySeed("TR", "Turkey", "Türkiyə", "Турция",
            "Mosques and minarets rise over the Bosphorus where Europe meets Asia.",
            "Avropa ilə Asiyanın qovuşduğu Bosfor üzərində məscidlər və minarələr ucalır.",
            "Мечети и минареты возвышаются над Босфором, где встречаются Европа и Азия.",
            "turkey-istanbul.jpg", new[]
            {
                C("Istanbul", "İstanbul", "Стамбул", new[] { "Istanbul", "Stambul" }, A("IST", "Istanbul Airport", "Europe/Istanbul")),
                C("Ankara", "Ankara", "Анкара", new[] { "Анкары", "Анкару" }, A("ESB", "Esenboğa Airport", "Europe/Istanbul")),
                C("Antalya", "Antalya", "Анталья", new[] { "Анталия", "Анталью", "Антальи", "Анталии", "Анталию" }, A("AYT", "Antalya Airport", "Europe/Istanbul"))
            }),
        new CatalogCountrySeed("AE", "United Arab Emirates", "Birləşmiş Ərəb Əmirlikləri", "Объединённые Арабские Эмираты",
            "A skyline of glass towers led by the Burj Khalifa, between desert and sea.",
            "Səhra ilə dəniz arasında Burc Xəlifənin başçılıq etdiyi şüşə göydələnlər silueti.",
            "Силуэт стеклянных башен во главе с Бурдж-Халифой между пустыней и морем.",
            "uae-dubai.jpg", new[]
            {
                C("Dubai", "Dubay", "Дубай", new[] { "Dubai", "Дубаи", "Дубая", "Дубаю" }, A("DXB", "Dubai International Airport", "Asia/Dubai")),
                C("Abu Dhabi", "Abu-Dabi", "Абу-Даби", new[] { "Abu Dabi", "Abu Dhabi" }, A("AUH", "Zayed International Airport", "Asia/Dubai"))
            }),
        new CatalogCountrySeed("GB", "United Kingdom", "Birləşmiş Krallıq", "Великобритания",
            "Big Ben and the Thames: royal history in the heart of London.",
            "Biq Ben və Temza: Londonun mərkəzində kral tarixi.",
            "Биг-Бен и Темза: королевская история в самом сердце Лондона.",
            "uk-london.jpg", new[]
            {
                C("London", "London", "Лондон", new[] { "Londra" }, A("LHR", "Heathrow Airport", "Europe/London")),
                C("Manchester", "Mançester", "Манчестер", new[] { "Mancester", "Manchester" }, A("MAN", "Manchester Airport", "Europe/London"))
            }),
        new CatalogCountrySeed("FR", "France", "Fransa", "Франция",
            "The Eiffel Tower glowing over the Seine at dusk, and the sunny Riviera beyond.",
            "Qürub çağı Sena çayı üzərində parlayan Eyfel qülləsi və uzaqdakı Günəşli Rivyera.",
            "Эйфелева башня, сияющая над Сеной в сумерках, и солнечная Ривьера за ней.",
            "france-paris.jpg", new[]
            {
                C("Paris", "Paris", "Париж", None, A("CDG", "Paris Charles de Gaulle Airport", "Europe/Paris")),
                C("Nice", "Nitsa", "Ницца", new[] { "Nice", "Nis", "Ниццы", "Ниццу" }, A("NCE", "Nice Côte d'Azur Airport", "Europe/Paris"))
            }),
        new CatalogCountrySeed("DE", "Germany", "Almaniya", "Германия",
            "The Brandenburg Gate, Berlin's symbol of unity, and a modern financial hub in Frankfurt.",
            "Berlinin birlik rəmzi Brandenburq qapısı və Frankfurtda müasir maliyyə mərkəzi.",
            "Бранденбургские ворота, символ единства Берлина, и современный финансовый центр Франкфурт.",
            "germany-berlin.jpg", new[]
            {
                C("Berlin", "Berlin", "Берлин", None, A("BER", "Berlin Brandenburg Airport", "Europe/Berlin")),
                C("Frankfurt", "Frankfurt", "Франкфурт", new[] { "Frankfurt am Main" }, A("FRA", "Frankfurt Airport", "Europe/Berlin"))
            }),
        new CatalogCountrySeed("IT", "Italy", "İtaliya", "Италия",
            "The Colosseum at night: two thousand years of history, and Milan's fashion beyond.",
            "Gecə Kolizey: iki min illik tarix və onun ardında Milanın moda dünyası.",
            "Колизей ночью: две тысячи лет истории, а за ним мода Милана.",
            "italy-rome.jpg", new[]
            {
                C("Rome", "Roma", "Рим", new[] { "Roma" }, A("FCO", "Leonardo da Vinci–Fiumicino Airport", "Europe/Rome")),
                C("Milan", "Milan", "Милан", new[] { "Milano" }, A("MXP", "Milan Malpensa Airport", "Europe/Rome"))
            }),
        new CatalogCountrySeed("ES", "Spain", "İspaniya", "Испания",
            "Gaudí's Sagrada Família towers over Barcelona; Madrid keeps the capital's pace.",
            "Qaudinin Sagrada Familiyası Barselonanın üzərində ucalır; Madrid paytaxt ritmini saxlayır.",
            "Саграда Фамилия Гауди возвышается над Барселоной; Мадрид живёт в ритме столицы.",
            "spain-barcelona.jpg", new[]
            {
                C("Madrid", "Madrid", "Мадрид", None, A("MAD", "Adolfo Suárez Madrid–Barajas Airport", "Europe/Madrid")),
                C("Barcelona", "Barselona", "Барселона", new[] { "Barcelona", "Барселоны", "Барселону" }, A("BCN", "Josep Tarradellas Barcelona–El Prat Airport", "Europe/Madrid"))
            }),
        new CatalogCountrySeed("GE", "Georgia", "Gürcüstan", "Грузия",
            "Tbilisi's old town climbs the hill below Narikala, with wooden balconies and sulphur baths.",
            "Tbilisinin köhnə şəhəri Narikala qalasının altında dırmaşır: taxta eyvanlar və kükürdlü hamamlar.",
            "Старый Тбилиси поднимается по склону под Нарикалой: деревянные балконы и серные бани.",
            "georgia-tbilisi.jpg", new[]
            {
                C("Tbilisi", "Tbilisi", "Тбилиси", new[] { "Tiflis" }, A("TBS", "Tbilisi International Airport", "Asia/Tbilisi")),
                C("Batumi", "Batumi", "Батуми", None, A("BUS", "Batumi International Airport", "Asia/Tbilisi"))
            }),
        new CatalogCountrySeed("QA", "Qatar", "Qətər", "Катар",
            "The West Bay towers of Doha rise straight from the waters of the Gulf.",
            "Dohanın Qərb Körfəzi göydələnləri birbaşa Fars körfəzinin sularından ucalır.",
            "Башни Уэст-Бэй в Дохе поднимаются прямо из вод Персидского залива.",
            "qatar-doha.jpg", new[]
            {
                C("Doha", "Doha", "Доха", new[] { "Дохи", "Доху" }, A("DOH", "Hamad International Airport", "Asia/Qatar"))
            }),
        new CatalogCountrySeed("SA", "Saudi Arabia", "Səudiyyə Ərəbistanı", "Саудовская Аравия",
            "Riyadh's skyline of the Kingdom Centre and the new financial district at twilight.",
            "Ər-Riyadın Krallıq Mərkəzi və yeni maliyyə rayonundan ibarət silueti alaqaranlıqda.",
            "Силуэт Эр-Рияда с башней Кингдом-центр и новым финансовым районом в сумерках.",
            "saudi-arabia-riyadh.jpg", new[]
            {
                C("Riyadh", "Ər-Riyad", "Эр-Рияд", new[] { "Er Riyad", "Riyad", "Рияд" }, A("RUH", "King Khalid International Airport", "Asia/Riyadh")),
                C("Jeddah", "Cidda", "Джидда", new[] { "Jidda", "Jeddah", "Jiddah", "Джидды", "Джидду" }, A("JED", "King Abdulaziz International Airport", "Asia/Riyadh"))
            }),
        new CatalogCountrySeed("CH", "Switzerland", "İsveçrə", "Швейцария",
            "Zurich's old town along the Limmat, with the Alps a short ride away.",
            "Sürixin Limmat boyunca köhnə şəhəri; Alp dağları isə yaxındadır.",
            "Старый Цюрих вдоль Лиммата, а Альпы совсем рядом.",
            "switzerland-zurich.jpg", new[]
            {
                C("Zurich", "Sürix", "Цюрих", new[] { "Zürich", "Surix" }, A("ZRH", "Zurich Airport", "Europe/Zurich")),
                C("Geneva", "Cenevrə", "Женева", new[] { "Genève", "Geneva", "Cenevre", "Женевы", "Женеву" }, A("GVA", "Geneva Airport", "Europe/Zurich"))
            }),
        new CatalogCountrySeed("NL", "Netherlands", "Niderland", "Нидерланды",
            "Amsterdam's canal houses leaning over the water, built in the Golden Age.",
            "Qızıl dövrdə tikilmiş, suyun üzərinə əyilən Amsterdam kanal evləri.",
            "Амстердамские дома на каналах, склонившиеся над водой, построены в Золотой век.",
            "netherlands-amsterdam.jpg", new[]
            {
                C("Amsterdam", "Amsterdam", "Амстердам", None, A("AMS", "Amsterdam Airport Schiphol", "Europe/Amsterdam"))
            }),
        new CatalogCountrySeed("US", "United States", "Amerika Birləşmiş Ştatları", "США",
            "Manhattan at sunset, with the Empire State Building lit over the city.",
            "Gün batımında Manhetten, şəhərin üzərində işıqlanan Empayr Stat Bildinqlə.",
            "Манхэттен на закате, Эмпайр-стейт-билдинг, подсвеченный над городом.",
            "usa-new-york.jpg", new[]
            {
                C("New York", "Nyu-York", "Нью-Йорк", new[] { "Nyu York", "New York City", "Нью Йорк" }, A("JFK", "John F. Kennedy International Airport", "America/New_York")),
                C("Los Angeles", "Los-Anceles", "Лос-Анджелес", new[] { "Los Anceles", "LA", "Лос Анджелес" }, A("LAX", "Los Angeles International Airport", "America/Los_Angeles"))
            }),
        new CatalogCountrySeed("CA", "Canada", "Kanada", "Канада",
            "The CN Tower and Toronto's skyline across Lake Ontario at dusk.",
            "Qürub çağı Ontario gölünün o tayında CN qülləsi və Torontonun silueti.",
            "Си-Эн Тауэр и силуэт Торонто за озером Онтарио в сумерках.",
            "canada-toronto.jpg", new[]
            {
                C("Toronto", "Toronto", "Торонто", None, A("YYZ", "Toronto Pearson International Airport", "America/Toronto")),
                C("Montreal", "Monreal", "Монреаль", new[] { "Montréal", "Montreal", "Монреаля", "Монреалю" }, A("YUL", "Montréal–Trudeau International Airport", "America/Toronto"))
            }),
        new CatalogCountrySeed("JP", "Japan", "Yaponiya", "Япония",
            "Tokyo's sprawling skyline, with Mount Fuji on the horizon on a clear day.",
            "Tokionun geniş silueti; açıq havada üfüqdə Fuji dağı görünür.",
            "Бескрайний силуэт Токио, а в ясный день на горизонте виднеется гора Фудзи.",
            "japan-tokyo.jpg", new[]
            {
                C("Tokyo", "Tokio", "Токио", new[] { "Tokyo", "Tokio" }, A("HND", "Tokyo Haneda Airport", "Asia/Tokyo"), A("NRT", "Narita International Airport", "Asia/Tokyo")),
                C("Osaka", "Osaka", "Осака", new[] { "Осаки", "Осаку" }, A("KIX", "Kansai International Airport", "Asia/Tokyo"))
            }),
        new CatalogCountrySeed("CN", "China", "Çin", "Китай",
            "Shanghai's Pudong skyline along the Huangpu River, crowned by the Oriental Pearl Tower.",
            "Huanpu çayı boyunca Şanxayın Pudong silueti, onun tacı Şərq mirvarisi televiziya qülləsidir.",
            "Силуэт шанхайского Пудуна вдоль реки Хуанпу, увенчанный телебашней «Восточная жемчужина».",
            "china-shanghai.jpg", new[]
            {
                C("Beijing", "Pekin", "Пекин", new[] { "Peking", "Beijing" }, A("PEK", "Beijing Capital International Airport", "Asia/Shanghai")),
                C("Shanghai", "Şanxay", "Шанхай", new[] { "Shanghai", "Sanxay", "Шанхая", "Шанхаю" }, A("PVG", "Shanghai Pudong International Airport", "Asia/Shanghai"))
            }),
        new CatalogCountrySeed("SG", "Singapore", "Sinqapur", "Сингапур",
            "Marina Bay: the lotus-shaped ArtScience Museum beside the city's glittering waterfront.",
            "Marina Bay: şəhərin parıldayan sahilində nilufər formalı ArtScience muzeyi.",
            "Марина-Бей: музей АртСайенс в форме лотоса на сверкающей набережной города.",
            "singapore-marina-bay.jpg", new[]
            {
                C("Singapore", "Sinqapur", "Сингапур", new[] { "Singapur" }, A("SIN", "Singapore Changi Airport", "Asia/Singapore"))
            }),
        new CatalogCountrySeed("TH", "Thailand", "Tailand", "Таиланд",
            "Gilded temples such as Wat Arun in Bangkok, and the beaches of Phuket.",
            "Banqkokda Vat Arun kimi qızıllı məbədlər və Puketin çimərlikləri.",
            "Позолоченные храмы, такие как Ват Арун в Бангкоке, и пляжи Пхукета.",
            "thailand-bangkok.jpg", new[]
            {
                C("Bangkok", "Banqkok", "Бангкок", new[] { "Bankok", "Bangkok" }, A("BKK", "Suvarnabhumi Airport", "Asia/Bangkok")),
                C("Phuket", "Puket", "Пхукет", new[] { "Phuket", "Пукет" }, A("HKT", "Phuket International Airport", "Asia/Bangkok"))
            }),
        new CatalogCountrySeed("MV", "Maldives", "Maldiv", "Мальдивы",
            "Overwater villas on a turquoise lagoon, reached by a wooden jetty.",
            "Firuzəyi laqunda taxta körpü ilə çatılan su üstü villalar.",
            "Виллы над водой на бирюзовой лагуне, к которым ведёт деревянный причал.",
            "maldives-male.jpg", new[]
            {
                C("Malé", "Male", "Мале", new[] { "Male", "Male City" }, A("MLE", "Velana International Airport", "Indian/Maldives"))
            })
    };
}
