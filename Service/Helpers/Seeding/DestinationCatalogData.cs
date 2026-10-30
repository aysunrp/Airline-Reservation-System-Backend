namespace Service.Helpers.Seeding;

public sealed record CatalogAirportSeed(string Code, string Name, string TimeZoneId);

public sealed record CatalogCitySeed(string Name, string NameAz, string NameRu, string[] Aliases, CatalogAirportSeed[] Airports)
{
    /// <summary>AEROVA's home base: kept in the catalog (search origin, airports, routes) but not shown as a place to travel to.</summary>
    public bool IsHub { get; init; }

    /// <summary>The city's own photograph (a file in the frontend's assets/images) and its short description in the three languages.</summary>
    public string ImageFile { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DescriptionAz { get; init; } = string.Empty;
    public string DescriptionRu { get; init; } = string.Empty;

    public CatalogCitySeed Info(string imageFile, string description, string descriptionAz, string descriptionRu)
        => this with { ImageFile = imageFile, Description = description, DescriptionAz = descriptionAz, DescriptionRu = descriptionRu };
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
                H("Baku", "Bakı", "Баку", new[] { "Baki", "Baku" }, A("GYD", "Heydar Aliyev International Airport", "Asia/Baku")).Info("azerbaijan-baku.jpg", "The Flame Towers, the walls of the Old City and the Caspian boulevard: AEROVA's home base.", "Alov Qüllələri, İçərişəhərin divarları və Xəzər bulvarı: AEROVA-nın ev bazası.", "Огненные башни, стены Старого города и каспийский бульвар: домашняя база AEROVA."),
                C("Ganja", "Gəncə", "Гянджа", new[] { "Gence", "Gandja", "Гянджа", "Гянджи", "Гянджу" }, A("GNJ", "Ganja International Airport", "Asia/Baku")).Info("azerbaijan-ganja.jpg", "Azerbaijan's second city, with shaded avenues, a tiled mosque and a long tradition of poetry and craft.", "Azərbaycanın ikinci böyük şəhəri: kölgəli küçələr, çinili məscid, şeir və sənətkarlıq ənənəsi.", "Второй по величине город Азербайджана: тенистые проспекты, мечеть с изразцами, традиции поэзии и ремёсел.")
            }),
        new CatalogCountrySeed("TR", "Turkey", "Türkiyə", "Турция",
            "Mosques and minarets rise over the Bosphorus where Europe meets Asia.",
            "Avropa ilə Asiyanın qovuşduğu Bosfor üzərində məscidlər və minarələr ucalır.",
            "Мечети и минареты возвышаются над Босфором, где встречаются Европа и Азия.",
            "turkey-istanbul.jpg", new[]
            {
                C("Istanbul", "İstanbul", "Стамбул", new[] { "Istanbul", "Stambul" }, A("IST", "Istanbul Airport", "Europe/Istanbul"), A("SAW", "Sabiha Gökçen International Airport", "Europe/Istanbul")).Info("turkey-istanbul.jpg", "Mosques and minarets rise over the Bosphorus, where Europe meets Asia.", "Avropa ilə Asiyanın qovuşduğu Bosfor üzərində məscidlər və minarələr ucalır.", "Мечети и минареты возвышаются над Босфором, где встречаются Европа и Азия."),
                C("Ankara", "Ankara", "Анкара", new[] { "Анкары", "Анкару" }, A("ESB", "Esenboğa Airport", "Europe/Istanbul")).Info("turkey-ankara.jpg", "Turkey's capital, with the monumental mausoleum of Atatürk, Anıtkabir.", "Türkiyənin paytaxtı: Atatürkün monumental məqbərəsi Anıtkabir burada yerləşir.", "Столица Турции: здесь находится монументальный мавзолей Ататюрка Аныткабир."),
                C("Antalya", "Antalya", "Анталья", new[] { "Анталия", "Анталью", "Антальи", "Анталии", "Анталию" }, A("AYT", "Antalya Airport", "Europe/Istanbul")).Info("turkey-antalya.jpg", "A Mediterranean city with an old harbour below the walls of Kaleiçi.", "Aralıq dənizi şəhəri: Kaleiçi divarlarının altında köhnə liman.", "Средиземноморский город со старой гаванью под стенами Калеичи.")
            }),
        new CatalogCountrySeed("AE", "United Arab Emirates", "Birləşmiş Ərəb Əmirlikləri", "Объединённые Арабские Эмираты",
            "A skyline of glass towers led by the Burj Khalifa, between desert and sea.",
            "Səhra ilə dəniz arasında Burc Xəlifənin başçılıq etdiyi şüşə göydələnlər silueti.",
            "Силуэт стеклянных башен во главе с Бурдж-Халифой между пустыней и морем.",
            "uae-dubai.jpg", new[]
            {
                C("Dubai", "Dubay", "Дубай", new[] { "Dubai", "Дубаи", "Дубая", "Дубаю" }, A("DXB", "Dubai International Airport", "Asia/Dubai")).Info("uae-dubai.jpg", "A skyline of glass towers led by the Burj Khalifa, between desert and sea.", "Səhra ilə dəniz arasında Burc Xəlifənin başçılıq etdiyi şüşə göydələnlər silueti.", "Силуэт стеклянных башен во главе с Бурдж-Халифой между пустыней и морем."),
                C("Abu Dhabi", "Abu-Dabi", "Абу-Даби", new[] { "Abu Dabi", "Abu Dhabi" }, A("AUH", "Zayed International Airport", "Asia/Dubai")).Info("uae-abu-dhabi.jpg", "The capital of the UAE, home of the Sheikh Zayed Grand Mosque.", "BƏƏ-nin paytaxtı: Şeyx Zayed Böyük Məscidi buradadır.", "Столица ОАЭ, где находится Большая мечеть шейха Зайда.")
            }),
        new CatalogCountrySeed("GB", "United Kingdom", "Birləşmiş Krallıq", "Великобритания",
            "Big Ben and the Thames: royal history in the heart of London.",
            "Biq Ben və Temza: Londonun mərkəzində kral tarixi.",
            "Биг-Бен и Темза: королевская история в самом сердце Лондона.",
            "uk-london.jpg", new[]
            {
                C("London", "London", "Лондон", new[] { "Londra" }, A("LHR", "Heathrow Airport", "Europe/London")).Info("uk-london.jpg", "Big Ben and the Thames: royal history in the heart of London.", "Biq Ben və Temza: Londonun mərkəzində kral tarixi.", "Биг-Бен и Темза: королевская история в самом сердце Лондона."),
                C("Manchester", "Mançester", "Манчестер", new[] { "Mancester", "Manchester" }, A("MAN", "Manchester Airport", "Europe/London")).Info("uk-manchester.jpg", "A city of Victorian architecture, led by its neo-Gothic Town Hall.", "Viktoriya dövrü memarlığı şəhəri: onun başında neoqotik Bələdiyyə binası durur.", "Город викторианской архитектуры, главное здание которого — неоготическая ратуша.")
            }),
        new CatalogCountrySeed("FR", "France", "Fransa", "Франция",
            "The Eiffel Tower glowing over the Seine at dusk, and the sunny Riviera beyond.",
            "Qürub çağı Sena çayı üzərində parlayan Eyfel qülləsi və uzaqdakı Günəşli Rivyera.",
            "Эйфелева башня, сияющая над Сеной в сумерках, и солнечная Ривьера за ней.",
            "france-paris.jpg", new[]
            {
                C("Paris", "Paris", "Париж", None, A("CDG", "Paris Charles de Gaulle Airport", "Europe/Paris")).Info("france-paris.jpg", "The Eiffel Tower glowing over the Seine at dusk.", "Qürub çağı Sena çayı üzərində parlayan Eyfel qülləsi.", "Эйфелева башня, сияющая над Сеной в сумерках."),
                C("Nice", "Nitsa", "Ницца", new[] { "Nice", "Nis", "Ниццы", "Ниццу" }, A("NCE", "Nice Côte d'Azur Airport", "Europe/Paris")).Info("france-nice.jpg", "The Bay of Angels and the seafront of the French Riviera.", "Mələklər körfəzi və Fransız Rivyerasının sahil xətti.", "Ангельская бухта и набережная Французской Ривьеры.")
            }),
        new CatalogCountrySeed("DE", "Germany", "Almaniya", "Германия",
            "The Brandenburg Gate, Berlin's symbol of unity, and a modern financial hub in Frankfurt.",
            "Berlinin birlik rəmzi Brandenburq qapısı və Frankfurtda müasir maliyyə mərkəzi.",
            "Бранденбургские ворота, символ единства Берлина, и современный финансовый центр Франкфурт.",
            "germany-berlin.jpg", new[]
            {
                C("Berlin", "Berlin", "Берлин", None, A("BER", "Berlin Brandenburg Airport", "Europe/Berlin")).Info("germany-berlin.jpg", "The Brandenburg Gate, Berlin's symbol of unity.", "Brandenburq qapısı, Berlinin birlik rəmzi.", "Бранденбургские ворота, символ единства Берлина."),
                C("Frankfurt", "Frankfurt", "Франкфурт", new[] { "Frankfurt am Main" }, A("FRA", "Frankfurt Airport", "Europe/Berlin")).Info("germany-frankfurt.jpg", "A skyline of towers along the River Main, Germany's financial centre.", "Main çayı boyunca göydələnlər silueti, Almaniyanın maliyyə mərkəzi.", "Силуэт небоскрёбов вдоль реки Майн, финансовый центр Германии.")
            }),
        new CatalogCountrySeed("IT", "Italy", "İtaliya", "Италия",
            "The Colosseum at night: two thousand years of history, and Milan's fashion beyond.",
            "Gecə Kolizey: iki min illik tarix və onun ardında Milanın moda dünyası.",
            "Колизей ночью: две тысячи лет истории, а за ним мода Милана.",
            "italy-rome.jpg", new[]
            {
                C("Rome", "Roma", "Рим", new[] { "Roma" }, A("FCO", "Leonardo da Vinci–Fiumicino Airport", "Europe/Rome")).Info("italy-rome.jpg", "The Colosseum at night: two thousand years of history.", "Gecə Kolizey: iki min illik tarix.", "Колизей ночью: две тысячи лет истории."),
                C("Milan", "Milan", "Милан", new[] { "Milano" }, A("MXP", "Milan Malpensa Airport", "Europe/Rome")).Info("italy-milan.jpg", "The Duomo, the Gothic cathedral at the centre of Milan.", "Duomo: Milanın mərkəzindəki qotik katedral.", "Дуомо — готический собор в центре Милана.")
            }),
        new CatalogCountrySeed("ES", "Spain", "İspaniya", "Испания",
            "Gaudí's Sagrada Família towers over Barcelona; Madrid keeps the capital's pace.",
            "Qaudinin Sagrada Familiyası Barselonanın üzərində ucalır; Madrid paytaxt ritmini saxlayır.",
            "Саграда Фамилия Гауди возвышается над Барселоной; Мадрид живёт в ритме столицы.",
            "spain-madrid.jpg", new[]
            {
                C("Madrid", "Madrid", "Мадрид", None, A("MAD", "Adolfo Suárez Madrid–Barajas Airport", "Europe/Madrid")).Info("spain-madrid.jpg", "The Puerta de Alcalá, a neoclassical gate in the centre of Spain's capital.", "Puerta de Alkala: İspaniya paytaxtının mərkəzindəki neoklassik qapı.", "Ворота Алькала — неоклассические ворота в центре столицы Испании."),
                C("Barcelona", "Barselona", "Барселона", new[] { "Barcelona", "Барселоны", "Барселону" }, A("BCN", "Josep Tarradellas Barcelona–El Prat Airport", "Europe/Madrid")).Info("spain-barcelona.jpg", "Gaudí's Sagrada Família towers over the city.", "Qaudinin Sagrada Familiyası şəhərin üzərində ucalır.", "Саграда Фамилия Гауди возвышается над городом.")
            }),
        new CatalogCountrySeed("GE", "Georgia", "Gürcüstan", "Грузия",
            "Tbilisi's old town climbs the hill below Narikala, with wooden balconies and sulphur baths.",
            "Tbilisinin köhnə şəhəri Narikala qalasının altında dırmaşır: taxta eyvanlar və kükürdlü hamamlar.",
            "Старый Тбилиси поднимается по склону под Нарикалой: деревянные балконы и серные бани.",
            "georgia-tbilisi.jpg", new[]
            {
                C("Tbilisi", "Tbilisi", "Тбилиси", new[] { "Tiflis" }, A("TBS", "Tbilisi International Airport", "Asia/Tbilisi")).Info("georgia-tbilisi.jpg", "The old town climbs the hill below Narikala, beside the Kura River.", "Köhnə şəhər Kür çayının kənarında Narikala qalasının altındakı yamacla yüksəlir.", "Старый город поднимается по склону под Нарикалой, рядом с рекой Кура."),
                C("Batumi", "Batumi", "Батуми", None, A("BUS", "Batumi International Airport", "Asia/Tbilisi")).Info("georgia-batumi.jpg", "A Black Sea port city, with the Alphabetic Tower on its seafront.", "Qara dəniz liman şəhəri: sahildə Əlifba qülləsi ucalır.", "Портовый город на Чёрном море с Алфавитной башней на набережной.")
            }),
        new CatalogCountrySeed("QA", "Qatar", "Qətər", "Катар",
            "The West Bay towers of Doha rise straight from the waters of the Gulf.",
            "Dohanın Qərb Körfəzi göydələnləri birbaşa Fars körfəzinin sularından ucalır.",
            "Башни Уэст-Бэй в Дохе поднимаются прямо из вод Персидского залива.",
            "qatar-doha.jpg", new[]
            {
                C("Doha", "Doha", "Доха", new[] { "Дохи", "Доху" }, A("DOH", "Hamad International Airport", "Asia/Qatar")).Info("qatar-doha.jpg", "The West Bay towers rise straight from the waters of the Gulf.", "Qərb Körfəzi göydələnləri birbaşa Fars körfəzinin sularından ucalır.", "Башни Уэст-Бэй поднимаются прямо из вод Персидского залива.")
            }),
        new CatalogCountrySeed("SA", "Saudi Arabia", "Səudiyyə Ərəbistanı", "Саудовская Аравия",
            "Riyadh's skyline of the Kingdom Centre and the new financial district at twilight.",
            "Ər-Riyadın Krallıq Mərkəzi və yeni maliyyə rayonundan ibarət silueti alaqaranlıqda.",
            "Силуэт Эр-Рияда с башней Кингдом-центр и новым финансовым районом в сумерках.",
            "saudi-arabia-riyadh.jpg", new[]
            {
                C("Riyadh", "Ər-Riyad", "Эр-Рияд", new[] { "Er Riyad", "Riyad", "Рияд" }, A("RUH", "King Khalid International Airport", "Asia/Riyadh")).Info("saudi-arabia-riyadh.jpg", "The capital of Saudi Arabia, its skyline lit at twilight.", "Səudiyyə Ərəbistanının paytaxtı, alaqaranlıqda işıqlanan siluet.", "Столица Саудовской Аравии, её силуэт в сумеречных огнях."),
                C("Jeddah", "Cidda", "Джидда", new[] { "Jidda", "Jeddah", "Jiddah", "Джидды", "Джидду" }, A("JED", "King Abdulaziz International Airport", "Asia/Riyadh")).Info("saudi-arabia-jeddah.jpg", "A Red Sea port city, with the old gates and houses of Al-Balad.", "Qırmızı dəniz liman şəhəri: Əl-Baladın qədim qapıları və evləri.", "Порт на Красном море с древними воротами и домами квартала Аль-Балад.")
            }),
        new CatalogCountrySeed("CH", "Switzerland", "İsveçrə", "Швейцария",
            "Zurich's old town along the Limmat, with the Alps a short ride away.",
            "Sürixin Limmat boyunca köhnə şəhəri; Alp dağları isə yaxındadır.",
            "Старый Цюрих вдоль Лиммата, а Альпы совсем рядом.",
            "switzerland-zurich.jpg", new[]
            {
                C("Zurich", "Sürix", "Цюрих", new[] { "Zürich", "Surix" }, A("ZRH", "Zurich Airport", "Europe/Zurich")).Info("switzerland-zurich.jpg", "The old town along the Limmat, with the Alps a short ride away.", "Limmat boyunca köhnə şəhər; Alp dağları isə yaxındadır.", "Старый город вдоль Лиммата, а Альпы совсем рядом."),
                C("Geneva", "Cenevrə", "Женева", new[] { "Genève", "Geneva", "Cenevre", "Женевы", "Женеву" }, A("GVA", "Geneva Airport", "Europe/Zurich")).Info("switzerland-geneva.jpg", "The Jet d'Eau rising from the lake, with the Alps behind.", "Göldən qalxan Jet d'Eau fontanı, arxada Alp dağları.", "Фонтан Жё-д'О над озером, а за ним Альпы.")
            }),
        new CatalogCountrySeed("NL", "Netherlands", "Niderland", "Нидерланды",
            "Amsterdam's canal houses leaning over the water, built in the Golden Age.",
            "Qızıl dövrdə tikilmiş, suyun üzərinə əyilən Amsterdam kanal evləri.",
            "Амстердамские дома на каналах, склонившиеся над водой, построены в Золотой век.",
            "netherlands-amsterdam.jpg", new[]
            {
                C("Amsterdam", "Amsterdam", "Амстердам", None, A("AMS", "Amsterdam Airport Schiphol", "Europe/Amsterdam")).Info("netherlands-amsterdam.jpg", "Canal houses of the Golden Age leaning over the water.", "Qızıl dövrdə tikilmiş, suyun üzərinə əyilən kanal evləri.", "Дома Золотого века на каналах, склонившиеся над водой.")
            }),
        new CatalogCountrySeed("US", "United States", "Amerika Birləşmiş Ştatları", "США",
            "Manhattan at sunset, with the Empire State Building lit over the city.",
            "Gün batımında Manhetten, şəhərin üzərində işıqlanan Empayr Stat Bildinqlə.",
            "Манхэттен на закате, Эмпайр-стейт-билдинг, подсвеченный над городом.",
            "usa-new-york.jpg", new[]
            {
                C("New York", "Nyu-York", "Нью-Йорк", new[] { "Nyu York", "New York City", "Нью Йорк" }, A("JFK", "John F. Kennedy International Airport", "America/New_York")).Info("usa-new-york.jpg", "Manhattan at sunset, with the Empire State Building lit over the city.", "Gün batımında Manhetten, şəhərin üzərində işıqlanan Empayr Stat Bildinqlə.", "Манхэттен на закате, Эмпайр-стейт-билдинг, подсвеченный над городом."),
                C("Los Angeles", "Los-Anceles", "Лос-Анджелес", new[] { "Los Anceles", "LA", "Лос Анджелес" }, A("LAX", "Los Angeles International Airport", "America/Los_Angeles")).Info("usa-los-angeles.jpg", "Downtown towers under a Pacific sky.", "Sakit okean səması altında şəhər mərkəzinin göydələnləri.", "Небоскрёбы даунтауна под небом Тихоокеанского побережья.")
            }),
        new CatalogCountrySeed("CA", "Canada", "Kanada", "Канада",
            "The CN Tower and Toronto's skyline across Lake Ontario at dusk.",
            "Qürub çağı Ontario gölünün o tayında CN qülləsi və Torontonun silueti.",
            "Си-Эн Тауэр и силуэт Торонто за озером Онтарио в сумерках.",
            "canada-toronto.jpg", new[]
            {
                C("Toronto", "Toronto", "Торонто", None, A("YYZ", "Toronto Pearson International Airport", "America/Toronto")).Info("canada-toronto.jpg", "The CN Tower and Toronto's skyline across Lake Ontario at dusk.", "Qürub çağı Ontario gölünün o tayında CN qülləsi və Torontonun silueti.", "Си-Эн Тауэр и силуэт Торонто за озером Онтарио в сумерках."),
                C("Montreal", "Monreal", "Монреаль", new[] { "Montréal", "Montreal", "Монреаля", "Монреалю" }, A("YUL", "Montréal–Trudeau International Airport", "America/Toronto")).Info("canada-montreal.jpg", "A skyline of towers on the island city, seen from Mount Royal.", "Adadakı şəhərin Mont-Royal təpəsindən görünən göydələn silueti.", "Силуэт башен города на острове, вид с горы Мон-Руаяль.")
            }),
        new CatalogCountrySeed("JP", "Japan", "Yaponiya", "Япония",
            "Tokyo's sprawling skyline, with Mount Fuji on the horizon on a clear day.",
            "Tokionun geniş silueti; açıq havada üfüqdə Fuji dağı görünür.",
            "Бескрайний силуэт Токио, а в ясный день на горизонте виднеется гора Фудзи.",
            "japan-tokyo.jpg", new[]
            {
                C("Tokyo", "Tokio", "Токио", new[] { "Tokyo", "Tokio" }, A("HND", "Tokyo Haneda Airport", "Asia/Tokyo"), A("NRT", "Narita International Airport", "Asia/Tokyo")).Info("japan-tokyo.jpg", "Tokyo's sprawling skyline, with Mount Fuji on the horizon on a clear day.", "Tokionun geniş silueti; açıq havada üfüqdə Fuji dağı görünür.", "Бескрайний силуэт Токио, а в ясный день на горизонте виднеется гора Фудзи."),
                C("Osaka", "Osaka", "Осака", new[] { "Осаки", "Осаку" }, A("KIX", "Kansai International Airport", "Asia/Tokyo")).Info("japan-osaka.jpg", "Osaka Castle, with its stone walls, among the towers of the business district.", "Osaka qalası, daş divarları ilə, iş rayonunun göydələnləri arasında.", "Замок Осаки с каменными стенами среди башен делового района.")
            }),
        new CatalogCountrySeed("CN", "China", "Çin", "Китай",
            "Shanghai's Pudong skyline along the Huangpu River, crowned by the Oriental Pearl Tower.",
            "Huanpu çayı boyunca Şanxayın Pudong silueti, onun tacı Şərq mirvarisi televiziya qülləsidir.",
            "Силуэт шанхайского Пудуна вдоль реки Хуанпу, увенчанный телебашней «Восточная жемчужина».",
            "china-beijing.jpg", new[]
            {
                C("Beijing", "Pekin", "Пекин", new[] { "Peking", "Beijing" }, A("PEK", "Beijing Capital International Airport", "Asia/Shanghai")).Info("china-beijing.jpg", "The Forbidden City, the former imperial palace at the centre of the capital.", "Qadağan olunmuş şəhər: paytaxtın mərkəzindəki keçmiş imperator sarayı.", "Запретный город — бывший императорский дворец в центре столицы."),
                C("Shanghai", "Şanxay", "Шанхай", new[] { "Shanghai", "Sanxay", "Шанхая", "Шанхаю" }, A("PVG", "Shanghai Pudong International Airport", "Asia/Shanghai")).Info("china-shanghai.jpg", "The Pudong skyline along the Huangpu River, crowned by the Oriental Pearl Tower.", "Huanpu çayı boyunca Pudong silueti, onun tacı Şərq mirvarisi televiziya qülləsidir.", "Силуэт Пудуна вдоль реки Хуанпу, увенчанный телебашней «Восточная жемчужина».")
            }),
        new CatalogCountrySeed("SG", "Singapore", "Sinqapur", "Сингапур",
            "Marina Bay: the lotus-shaped ArtScience Museum beside the city's glittering waterfront.",
            "Marina Bay: şəhərin parıldayan sahilində nilufər formalı ArtScience muzeyi.",
            "Марина-Бей: музей АртСайенс в форме лотоса на сверкающей набережной города.",
            "singapore-marina-bay.jpg", new[]
            {
                C("Singapore", "Sinqapur", "Сингапур", new[] { "Singapur" }, A("SIN", "Singapore Changi Airport", "Asia/Singapore")).Info("singapore-marina-bay.jpg", "Marina Bay: the lotus-shaped ArtScience Museum beside the glittering waterfront.", "Marina Bay: parıldayan sahildə nilufər formalı ArtScience muzeyi.", "Марина-Бей: музей АртСайенс в форме лотоса на сверкающей набережной.")
            }),
        new CatalogCountrySeed("TH", "Thailand", "Tailand", "Таиланд",
            "Gilded temples such as Wat Arun in Bangkok, and the beaches of Phuket.",
            "Banqkokda Vat Arun kimi qızıllı məbədlər və Puketin çimərlikləri.",
            "Позолоченные храмы, такие как Ват Арун в Бангкоке, и пляжи Пхукета.",
            "thailand-bangkok.jpg", new[]
            {
                C("Bangkok", "Banqkok", "Бангкок", new[] { "Bankok", "Bangkok" }, A("BKK", "Suvarnabhumi Airport", "Asia/Bangkok")).Info("thailand-bangkok.jpg", "Gilded temples such as Wat Arun beside the river.", "Çayın kənarında Vat Arun kimi qızıllı məbədlər.", "Позолоченные храмы, такие как Ват Арун, у реки."),
                C("Phuket", "Puket", "Пхукет", new[] { "Phuket", "Пукет" }, A("HKT", "Phuket International Airport", "Asia/Bangkok")).Info("thailand-phuket.jpg", "Thailand's largest island, with quiet beaches on the Andaman Sea.", "Tailandın ən böyük adası: Andaman dənizi sahilində sakit çimərliklər.", "Крупнейший остров Таиланда с тихими пляжами на Андаманском море.")
            }),
        new CatalogCountrySeed("MV", "Maldives", "Maldiv", "Мальдивы",
            "Overwater villas on a turquoise lagoon, reached by a wooden jetty.",
            "Firuzəyi laqunda taxta körpü ilə çatılan su üstü villalar.",
            "Виллы над водой на бирюзовой лагуне, к которым ведёт деревянный причал.",
            "maldives-male.jpg", new[]
            {
                C("Malé", "Male", "Мале", new[] { "Male", "Male City" }, A("MLE", "Velana International Airport", "Indian/Maldives")).Info("maldives-male.jpg", "The gateway to the atolls: overwater villas on a turquoise lagoon.", "Atollara qapı: firuzəyi laqunda su üstü villalar.", "Врата к атоллам: виллы над водой на бирюзовой лагуне.")
            })
    };
}
