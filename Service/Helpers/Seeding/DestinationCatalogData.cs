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
/// The destination catalog: 15 countries, their cities and the airports that serve them. It is reference data, not demo data: it says
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
                C("Ganja", "Gəncə", "Гянджа", new[] { "Gence", "Gandja", "Гянджа", "Гянджи", "Гянджу" }, A("GNJ", "Ganja International Airport", "Asia/Baku")).Info("azerbaijan-ganja.jpg", "Azerbaijan's second city, with shaded avenues, a tiled mosque and a long tradition of poetry and craft.", "Azərbaycanın ikinci böyük şəhəri: kölgəli küçələr, çinili məscid, şeir və sənətkarlıq ənənəsi.", "Второй по величине город Азербайджана: тенистые проспекты, мечеть с изразцами, традиции поэзии и ремёсел."),
                C("Nakhchivan", "Naxçıvan", "Нахчыван", new[] { "Nakhichevan", "Nahcivan", "Нахичевань" }, A("NAJ", "Nakhchivan International Airport", "Asia/Baku")).Info("", "The capital of the Nakhchivan exclave, with the Momine Khatun Mausoleum in the Araz valley.", "Naxçıvan eksklavının paytaxtı: Araz vadisində Möminə Xatun türbəsi.", "Столица Нахчыванского эксклава: мавзолей Момине-хатун в долине Аракса."),
            }),
        new CatalogCountrySeed("TR", "Turkey", "Türkiyə", "Турция",
            "Mosques and minarets rise over the Bosphorus where Europe meets Asia.",
            "Avropa ilə Asiyanın qovuşduğu Bosfor üzərində məscidlər və minarələr ucalır.",
            "Мечети и минареты возвышаются над Босфором, где встречаются Европа и Азия.",
            "turkey-istanbul.jpg", new[]
            {
                C("Istanbul", "İstanbul", "Стамбул", new[] { "Istanbul", "Stambul" }, A("IST", "Istanbul Airport", "Europe/Istanbul")).Info("turkey-istanbul.jpg", "Mosques and minarets rise over the Bosphorus, where Europe meets Asia.", "Avropa ilə Asiyanın qovuşduğu Bosfor üzərində məscidlər və minarələr ucalır.", "Мечети и минареты возвышаются над Босфором, где встречаются Европа и Азия."),
                C("Antalya", "Antalya", "Анталья", new[] { "Анталия", "Анталью", "Антальи", "Анталии", "Анталию" }, A("AYT", "Antalya Airport", "Europe/Istanbul")).Info("turkey-antalya.jpg", "A Mediterranean city with an old harbour below the walls of Kaleiçi.", "Aralıq dənizi şəhəri: Kaleiçi divarlarının altında köhnə liman.", "Средиземноморский город со старой гаванью под стенами Калеичи."),
                C("Bodrum", "Bodrum", "Бодрум", new[] { "Milas" }, A("BJV", "Milas-Bodrum Airport", "Europe/Istanbul")).Info("", "A white-washed Aegean resort town guarded by the Castle of St Peter and its marina.", "Ağ evli Egey kurort şəhəri; Müqəddəs Pyotr qalası və yaxta limanı ilə.", "Белоснежный курорт на Эгейском море с замком Святого Петра и яхтенной гаванью."),
            }),
        new CatalogCountrySeed("AE", "United Arab Emirates", "Birləşmiş Ərəb Əmirlikləri", "Объединённые Арабские Эмираты",
            "A skyline of glass towers led by the Burj Khalifa, between desert and sea.",
            "Səhra ilə dəniz arasında Burc Xəlifənin başçılıq etdiyi şüşə göydələnlər silueti.",
            "Силуэт стеклянных башен во главе с Бурдж-Халифой между пустыней и морем.",
            "uae-dubai.jpg", new[]
            {
                C("Dubai", "Dubay", "Дубай", new[] { "Dubai", "Дубаи", "Дубая", "Дубаю" }, A("DXB", "Dubai International Airport", "Asia/Dubai")).Info("uae-dubai.jpg", "A skyline of glass towers led by the Burj Khalifa, between desert and sea.", "Səhra ilə dəniz arasında Burc Xəlifənin başçılıq etdiyi şüşə göydələnlər silueti.", "Силуэт стеклянных башен во главе с Бурдж-Халифой между пустыней и морем."),
                C("Abu Dhabi", "Abu-Dabi", "Абу-Даби", new[] { "Abu Dabi", "Abu Dhabi" }, A("AUH", "Zayed International Airport", "Asia/Dubai")).Info("uae-abu-dhabi.jpg", "The capital of the UAE, home of the Sheikh Zayed Grand Mosque.", "BƏƏ-nin paytaxtı: Şeyx Zayed Böyük Məscidi buradadır.", "Столица ОАЭ, где находится Большая мечеть шейха Зайда."),
            }),
        new CatalogCountrySeed("GB", "United Kingdom", "Birləşmiş Krallıq", "Великобритания",
            "Big Ben and the Thames: royal history in the heart of London.",
            "Biq Ben və Temza: Londonun mərkəzində kral tarixi.",
            "Биг-Бен и Темза: королевская история в самом сердце Лондона.",
            "uk-london.jpg", new[]
            {
                C("London", "London", "Лондон", new[] { "Londra" }, A("LHR", "Heathrow Airport", "Europe/London")).Info("uk-london.jpg", "Big Ben and the Thames: royal history in the heart of London.", "Biq Ben və Temza: Londonun mərkəzində kral tarixi.", "Биг-Бен и Темза: королевская история в самом сердце Лондона."),
                C("Manchester", "Mançester", "Манчестер", new[] { "Mancester", "Manchester" }, A("MAN", "Manchester Airport", "Europe/London")).Info("uk-manchester.jpg", "A city of Victorian architecture, led by its neo-Gothic Town Hall.", "Viktoriya dövrü memarlığı şəhəri: onun başında neoqotik Bələdiyyə binası durur.", "Город викторианской архитектуры, главное здание которого — неоготическая ратуша."),
                C("Edinburgh", "Edinburq", "Эдинбург", new[] { "Edinburgh" }, A("EDI", "Edinburgh Airport", "Europe/London")).Info("", "Scotland's capital under its castle, with the Royal Mile and the old town.", "Qala altında Şotlandiyanın paytaxtı: Kral Mili və köhnə şəhər.", "Столица Шотландии под своим замком: Королевская миля и старый город."),
            }),
        new CatalogCountrySeed("FR", "France", "Fransa", "Франция",
            "The Eiffel Tower glowing over the Seine at dusk, and the sunny Riviera beyond.",
            "Qürub çağı Sena çayı üzərində parlayan Eyfel qülləsi və uzaqdakı Günəşli Rivyera.",
            "Эйфелева башня, сияющая над Сеной в сумерках, и солнечная Ривьера за ней.",
            "france-paris.jpg", new[]
            {
                C("Paris", "Paris", "Париж", None, A("CDG", "Paris Charles de Gaulle Airport", "Europe/Paris")).Info("france-paris.jpg", "The Eiffel Tower glowing over the Seine at dusk.", "Qürub çağı Sena çayı üzərində parlayan Eyfel qülləsi.", "Эйфелева башня, сияющая над Сеной в сумерках."),
                C("Marseille", "Marsel", "Марсель", new[] { "Marseille", "Marseilles" }, A("MRS", "Marseille Provence Airport", "Europe/Paris")).Info("", "France's oldest port, with the Vieux-Port and Notre-Dame de la Garde above it.", "Fransanın ən qədim limanı: Vyö-Por və üzərində Notr-Dam-de-la-Qard bazilikası.", "Старейший порт Франции: Старая гавань и базилика Нотр-Дам-де-ла-Гард над ней."),
            }),
        new CatalogCountrySeed("DE", "Germany", "Almaniya", "Германия",
            "The Brandenburg Gate, Berlin's symbol of unity, and a modern financial hub in Frankfurt.",
            "Berlinin birlik rəmzi Brandenburq qapısı və Frankfurtda müasir maliyyə mərkəzi.",
            "Бранденбургские ворота, символ единства Берлина, и современный финансовый центр Франкфурт.",
            "germany-berlin.jpg", new[]
            {
                C("Berlin", "Berlin", "Берлин", None, A("BER", "Berlin Brandenburg Airport", "Europe/Berlin")).Info("germany-berlin.jpg", "The Brandenburg Gate, Berlin's symbol of unity.", "Brandenburq qapısı, Berlinin birlik rəmzi.", "Бранденбургские ворота, символ единства Берлина."),
                C("Frankfurt", "Frankfurt", "Франкфурт", new[] { "Frankfurt am Main" }, A("FRA", "Frankfurt Airport", "Europe/Berlin")).Info("germany-frankfurt.jpg", "A skyline of towers along the River Main, Germany's financial centre.", "Main çayı boyunca göydələnlər silueti, Almaniyanın maliyyə mərkəzi.", "Силуэт небоскрёбов вдоль реки Майн, финансовый центр Германии."),
                C("Munich", "Münhen", "Мюнхен", new[] { "Munchen", "München", "Munih" }, A("MUC", "Munich Airport", "Europe/Berlin")).Info("", "The Bavarian capital of beer halls and the Marienplatz, with the Alps on the horizon.", "Pivə zalları və Marienplatz olan Bavariya paytaxtı; üfüqdə Alp dağları.", "Столица Баварии: пивные залы и Мариенплац, а на горизонте — Альпы."),
            }),
        new CatalogCountrySeed("IT", "Italy", "İtaliya", "Италия",
            "The Colosseum at night: two thousand years of history, and Milan's fashion beyond.",
            "Gecə Kolizey: iki min illik tarix və onun ardında Milanın moda dünyası.",
            "Колизей ночью: две тысячи лет истории, а за ним мода Милана.",
            "italy-rome.jpg", new[]
            {
                C("Rome", "Roma", "Рим", new[] { "Roma" }, A("FCO", "Leonardo da Vinci–Fiumicino Airport", "Europe/Rome")).Info("italy-rome.jpg", "The Colosseum at night: two thousand years of history.", "Gecə Kolizey: iki min illik tarix.", "Колизей ночью: две тысячи лет истории."),
                C("Milan", "Milan", "Милан", new[] { "Milano" }, A("MXP", "Milan Malpensa Airport", "Europe/Rome")).Info("italy-milan.jpg", "The Duomo, the Gothic cathedral at the centre of Milan.", "Duomo: Milanın mərkəzindəki qotik katedral.", "Дуомо — готический собор в центре Милана."),
                C("Venice", "Venetsiya", "Венеция", new[] { "Venezia", "Venedik" }, A("VCE", "Venice Marco Polo Airport", "Europe/Rome")).Info("", "A city of canals and bridges on a lagoon, with St Mark's Square at its heart.", "Laqun üzərində kanallar və körpülər şəhəri; mərkəzdə Müqəddəs Mark meydanı.", "Город каналов и мостов на лагуне, в центре — площадь Святого Марка."),
            }),
        new CatalogCountrySeed("ES", "Spain", "İspaniya", "Испания",
            "Gaudí's Sagrada Família towers over Barcelona; Madrid keeps the capital's pace.",
            "Qaudinin Sagrada Familiyası Barselonanın üzərində ucalır; Madrid paytaxt ritmini saxlayır.",
            "Саграда Фамилия Гауди возвышается над Барселоной; Мадрид живёт в ритме столицы.",
            "spain-madrid.jpg", new[]
            {
                C("Madrid", "Madrid", "Мадрид", None, A("MAD", "Adolfo Suárez Madrid–Barajas Airport", "Europe/Madrid")).Info("spain-madrid.jpg", "The Puerta de Alcalá, a neoclassical gate in the centre of Spain's capital.", "Puerta de Alkala: İspaniya paytaxtının mərkəzindəki neoklassik qapı.", "Ворота Алькала — неоклассические ворота в центре столицы Испании."),
                C("Barcelona", "Barselona", "Барселона", new[] { "Barcelona", "Барселоны", "Барселону" }, A("BCN", "Josep Tarradellas Barcelona–El Prat Airport", "Europe/Madrid")).Info("spain-barcelona.jpg", "Gaudí's Sagrada Família towers over the city.", "Qaudinin Sagrada Familiyası şəhərin üzərində ucalır.", "Саграда Фамилия Гауди возвышается над городом."),
                C("Malaga", "Malaqa", "Малага", new[] { "Malaga", "Málaga" }, A("AGP", "Malaga-Costa del Sol Airport", "Europe/Madrid")).Info("", "A Costa del Sol port beneath the Alcazaba, and the birthplace of Picasso.", "Alkasaba altında Kosta del Sol limanı və Pikassonun doğulduğu şəhər.", "Порт Коста-дель-Соль под Алькасабой и родина Пикассо."),
            }),
        new CatalogCountrySeed("JP", "Japan", "Yaponiya", "Япония",
            "Tokyo's sprawling skyline, with Mount Fuji on the horizon on a clear day.",
            "Tokionun geniş silueti; açıq havada üfüqdə Fuji dağı görünür.",
            "Бескрайний силуэт Токио, а в ясный день на горизонте виднеется гора Фудзи.",
            "japan-tokyo.jpg", new[]
            {
                C("Tokyo", "Tokio", "Токио", new[] { "Tokyo", "Tokio", "Tokiyo" }, A("HND", "Tokyo Haneda Airport", "Asia/Tokyo")).Info("japan-tokyo.jpg", "Tokyo's sprawling skyline, with Mount Fuji on the horizon on a clear day.", "Tokionun geniş silueti; açıq havada üfüqdə Fuji dağı görünür.", "Бескрайний силуэт Токио, а в ясный день на горизонте виднеется гора Фудзи."),
                C("Osaka", "Osaka", "Осака", new[] { "Осаки", "Осаку" }, A("KIX", "Kansai International Airport", "Asia/Tokyo")).Info("japan-osaka.jpg", "Osaka Castle, with its stone walls, among the towers of the business district.", "Osaka qalası, daş divarları ilə, iş rayonunun göydələnləri arasında.", "Замок Осаки с каменными стенами среди башен делового района."),
            }),
        new CatalogCountrySeed("MV", "Maldives", "Maldiv", "Мальдивы",
            "Overwater villas on a turquoise lagoon, reached by a wooden jetty.",
            "Firuzəyi laqunda taxta körpü ilə çatılan su üstü villalar.",
            "Виллы над водой на бирюзовой лагуне, к которым ведёт деревянный причал.",
            "maldives-male.jpg", new[]
            {
                C("Malé", "Male", "Мале", new[] { "Male", "Male City" }, A("MLE", "Velana International Airport", "Indian/Maldives")).Info("maldives-male.jpg", "The gateway to the atolls: overwater villas on a turquoise lagoon.", "Atollara qapı: firuzəyi laqunda su üstü villalar.", "Врата к атоллам: виллы над водой на бирюзовой лагуне.")
            }),
        new CatalogCountrySeed("AT", "Austria", "Avstriya", "Австрия",
            "Vienna's imperial palaces and coffee houses, with the Alps beyond.",
            "Vyananın imperiya sarayları və qəhvəxanaları, arxada isə Alp dağları.",
            "Императорские дворцы и кофейни Вены, а за ними Альпы.",
            "hero-aircraft.jpg", new[]
            {
                C("Vienna", "Vyana", "Вена", new[] { "Wien", "Vienna" }, A("VIE", "Vienna International Airport", "Europe/Vienna")).Info("hero-aircraft.jpg", "The imperial capital of palaces, opera and coffee houses on the Danube.", "Dunay sahilində saraylar, opera və qəhvəxanalar olan imperiya paytaxtı.", "Императорская столица дворцов, оперы и кофеен на Дунае."),
                C("Salzburg", "Zalsburq", "Зальцбург", new[] { "Salzburg" }, A("SZG", "Salzburg Airport", "Europe/Vienna")).Info("", "Mozart's birthplace beneath its fortress, with the Alps just beyond.", "Qala altında Motsartın doğulduğu şəhər, yaxınlıqda Alp dağları.", "Родина Моцарта под своей крепостью, а рядом — Альпы."),
            }),
        new CatalogCountrySeed("US", "United States", "Amerika Birləşmiş Ştatları", "Соединённые Штаты",
            "From Manhattan towers to Pacific boulevards: a continent of skylines.",
            "Manhetten qüllələrindən Sakit okean bulvarlarına: siluetlər qitəsi.",
            "От башен Манхэттена до тихоокеанских бульваров: континент небоскрёбов.",
            "", new[]
            {
                C("New York", "Nyu-York", "Нью-Йорк", new[] { "New York City", "NYC", "Нью Йорк" }, A("JFK", "John F. Kennedy International Airport", "America/New_York")).Info("", "Manhattan's skyline at golden hour: Broadway lights, Central Park and avenues that never sleep.", "Qızıl saatda Manhetten siluet: Brodvey işıqları, Mərkəzi Park və heç vaxt yatmayan prospektlər.", "Силуэт Манхэттена в золотой час: огни Бродвея, Центральный парк и проспекты, которые никогда не спят."),
                C("Los Angeles", "Los-Anceles", "Лос-Анджелес", new[] { "LA", "Los Anceles" }, A("LAX", "Los Angeles International Airport", "America/Los_Angeles")).Info("", "Sunlit boulevards, Pacific beaches and the glamour of Hollywood.", "Günəşli bulvarlar, Sakit okean çimərlikləri və Hollivudun parıltısı.", "Залитые солнцем бульвары, пляжи Тихого океана и блеск Голливуда."),
                C("Miami", "Mayami", "Майами", new[] { "Mayami" }, A("MIA", "Miami International Airport", "America/New_York")).Info("", "Art Deco facades, turquoise water and a seafront made for long evenings.", "Art Deko fasadları, firuzəyi su və uzun axşamlar üçün yaranmış sahil.", "Фасады ар-деко, бирюзовая вода и набережная для долгих вечеров."),
            }),
        new CatalogCountrySeed("GR", "Greece", "Yunanıstan", "Греция",
            "Whitewashed islands, ancient temples and the deep blue Aegean.",
            "Ağ adalar, qədim məbədlər və dərin mavi Egey.",
            "Белоснежные острова, древние храмы и синее Эгейское море.",
            "", new[]
            {
                C("Athens", "Afina", "Афины", new[] { "Athina", "Athens" }, A("ATH", "Athens International Airport Eleftherios Venizelos", "Europe/Athens")).Info("", "The Acropolis above a city of ancient stone, rooftop terraces and Aegean light.", "Qədim daşlar, dam terasları və Egey işığı şəhəri üzərində Akropol.", "Акрополь над городом древних камней, террас на крышах и эгейского света."),
                C("Santorini", "Santorini", "Санторини", new[] { "Thira", "Santorin" }, A("JTR", "Santorini (Thira) National Airport", "Europe/Athens")).Info("", "White villages on the caldera rim, with sunsets over the volcanic Aegean.", "Kalderanın kənarında ağ kəndlər və vulkanik Egey üzərində qürub.", "Белые деревушки на краю кальдеры и закаты над вулканическим Эгейским морем."),
                C("Mykonos", "Mikonos", "Миконос", new[] { "Mykonos", "Mikonos" }, A("JMK", "Mykonos Island National Airport", "Europe/Athens")).Info("", "Windmills, whitewashed lanes and beach clubs on the Cyclades' most glamorous island.", "Kiklad adalarının ən cazibədar adasında yel dəyirmanları, ağ küçələr və çimərlik klubları.", "Ветряные мельницы, белые улочки и пляжные клубы на самом гламурном острове Киклад."),
            }),
        new CatalogCountrySeed("TH", "Thailand", "Tailand", "Таиланд",
            "Golden temples, tropical bays and warm hospitality.",
            "Qızıllı məbədlər, tropik körfəzlər və səmimi qonaqpərvərlik.",
            "Золотые храмы, тропические бухты и радушное гостеприимство.",
            "", new[]
            {
                C("Bangkok", "Banqkok", "Бангкок", new[] { "Bankok", "Krung Thep" }, A("BKK", "Suvarnabhumi Airport", "Asia/Bangkok")).Info("", "Gilded temples, river-side hotels and a street-food scene known worldwide.", "Qızıllı məbədlər, çay sahilində otellər və dünyaca məşhur küçə yeməkləri.", "Золотые храмы, отели на берегу реки и всемирно известная уличная кухня."),
                C("Phuket", "Phuket", "Пхукет", new[] { "Phuket" }, A("HKT", "Phuket International Airport", "Asia/Bangkok")).Info("", "Andaman Sea bays, soft white sand and resorts above the water.", "Andaman dənizi körfəzləri, yumşaq ağ qum və suyun üstündə kurortlar.", "Бухты Андаманского моря, мягкий белый песок и курорты над водой."),
            }),
        new CatalogCountrySeed("PT", "Portugal", "Portuqaliya", "Португалия",
            "Atlantic light, tiled streets and the golden coast of the Algarve.",
            "Atlantik işığı, çinili küçələr və Alqarvenin qızılı sahili.",
            "Атлантический свет, улицы с азулежу и золотое побережье Алгарве.",
            "", new[]
            {
                C("Lisbon", "Lissabon", "Лиссабон", new[] { "Lisboa", "Lisbon" }, A("LIS", "Humberto Delgado Airport", "Europe/Lisbon")).Info("", "Pastel tiled facades, yellow trams and the Tagus glowing at sunset.", "Çinili pastel fasadlar, sarı tramvaylar və qürubda parlayan Taqus çayı.", "Фасады с азулежу пастельных тонов, жёлтые трамваи и Тежу в лучах заката."),
                C("Porto", "Porto", "Порту", new[] { "Oporto" }, A("OPO", "Francisco Sá Carneiro Airport", "Europe/Lisbon")).Info("", "Port-wine cellars along the Douro and the Ribeira's colourful riverfront.", "Duro çayı boyunca portveyn zirzəmiləri və Ribeyranın rəngarəng sahili.", "Погреба портвейна вдоль Дору и разноцветная набережная Рибейра."),
                C("Faro", "Faro", "Фару", new[] { "Algarve" }, A("FAO", "Faro Airport", "Europe/Lisbon")).Info("", "The gateway to the Algarve: golden cliffs, lagoons and quiet beaches.", "Alqarvenin qapısı: qızılı qayalar, laqunlar və sakit çimərliklər.", "Ворота в Алгарве: золотистые скалы, лагуны и тихие пляжи."),
            }),
    };

    /// <summary>
    /// Countries that left the catalog when it was narrowed to the fifteen above. They are switched off (IsActive = false) with their cities,
    /// airports, routes and schedules; nothing is deleted, so bookings, payments and every relation stay intact. Switching one on again in the
    /// admin panel is not undone by the seed.
    /// </summary>
    public static readonly IReadOnlyList<string> RetiredCountryCodes = new[] { "QA", "SA", "CH", "CA", "CN", "SG", "GE", "NL", "PL", "KZ", "UZ" };

    /// <summary>Airports of countries that stay in the catalog but left it as destinations. Switched off with their city (when no other airport of it stays) and routes.</summary>
    public static readonly IReadOnlyList<string> RetiredAirportCodes = new[]
    {
        "SHJ", "RKT", "AAN", "GRZ", "INN", "LNZ", "HAM", "DUS", "CGN", "VLC", "SVQ", "PMI", "LYS", "TLS", "BOD", "BHX", "GLA", "BRS",
        "NAP", "BLQ", "FLR", "NRT", "NGO", "FUK", "CTS", "SAW", "ESB", "ADB", "TZX", "TBS"
    };

    /// <summary>
    /// Routes of an earlier demo network that AEROVA no longer sells: the far destinations were first given invented direct flights from Baku
    /// and are reached through Istanbul instead. The routes and their schedules are switched off (never deleted), both directions.
    /// </summary>
    public static readonly IReadOnlyList<(string Origin, string Destination)> RetiredRoutes = new (string, string)[]
    {
        ("GYD", "JFK"), ("JFK", "GYD"), ("GYD", "BKK"), ("BKK", "GYD"), ("GYD", "LIS"), ("LIS", "GYD"), ("GYD", "OPO"), ("OPO", "GYD"),
        ("LIS", "FAO"), ("FAO", "LIS"), ("BKK", "HKT"), ("HKT", "BKK"), ("JFK", "LAX"), ("LAX", "JFK"), ("JFK", "MIA"), ("MIA", "JFK")
    };

    /// <summary>Countries that had been switched off earlier and are in the catalog again: the country, its catalog cities and their airports are switched back on.</summary>
    public static readonly IReadOnlyList<string> ReactivatedCountryCodes = new[] { "US", "TH" };
}
