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
/// The destination catalog: 16 countries, their cities and the airports that serve them. It is reference data, not demo data: it says
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
                C("Istanbul", "İstanbul", "Стамбул", new[] { "Istanbul", "Stambul" }, A("IST", "Istanbul Airport", "Europe/Istanbul"), A("SAW", "Sabiha Gökçen International Airport", "Europe/Istanbul")).Info("turkey-istanbul.jpg", "Mosques and minarets rise over the Bosphorus, where Europe meets Asia.", "Avropa ilə Asiyanın qovuşduğu Bosfor üzərində məscidlər və minarələr ucalır.", "Мечети и минареты возвышаются над Босфором, где встречаются Европа и Азия."),
                C("Ankara", "Ankara", "Анкара", new[] { "Анкары", "Анкару" }, A("ESB", "Esenboğa Airport", "Europe/Istanbul")).Info("turkey-ankara.jpg", "Turkey's capital, with the monumental mausoleum of Atatürk, Anıtkabir.", "Türkiyənin paytaxtı: Atatürkün monumental məqbərəsi Anıtkabir burada yerləşir.", "Столица Турции: здесь находится монументальный мавзолей Ататюрка Аныткабир."),
                C("Antalya", "Antalya", "Анталья", new[] { "Анталия", "Анталью", "Антальи", "Анталии", "Анталию" }, A("AYT", "Antalya Airport", "Europe/Istanbul")).Info("turkey-antalya.jpg", "A Mediterranean city with an old harbour below the walls of Kaleiçi.", "Aralıq dənizi şəhəri: Kaleiçi divarlarının altında köhnə liman.", "Средиземноморский город со старой гаванью под стенами Калеичи."),
                C("Izmir", "İzmir", "Измир", new[] { "Izmir", "Smyrna" }, A("ADB", "Izmir Adnan Menderes Airport", "Europe/Istanbul")).Info("", "A bright Aegean port with a lively waterfront and the Clock Tower in Konak Square.", "Əcə dənizi sahilində işıqlı liman şəhəri: canlı sahil və Konak meydanında Saat qülləsi.", "Светлый порт на Эгейском море: оживлённая набережная и Часовая башня на площади Конак."),
                C("Trabzon", "Trabzon", "Трабзон", new[] { "Trebizond" }, A("TZX", "Trabzon Airport", "Europe/Istanbul")).Info("", "A Black Sea city of green hills, with the Sumela Monastery clinging to a cliff nearby.", "Yaşıl təpələrlə əhatə olunmuş Qara dəniz şəhəri; yaxınlıqda qayaya yapışmış Sümela monastırı.", "Черноморский город зелёных холмов; неподалёку монастырь Сумела на скале."),
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
                C("Sharjah", "Şarcə", "Шарджа", new[] { "Sharja", "Sarca" }, A("SHJ", "Sharjah International Airport", "Asia/Dubai")).Info("", "The cultural capital of the UAE, with museums, souks and the Al Noor Mosque on the lagoon.", "BƏƏ-nin mədəniyyət paytaxtı: muzeylər, bazarlar və laqun sahilində Əl-Nur məscidi.", "Культурная столица ОАЭ: музеи, сук и мечеть Аль-Нур на берегу лагуны."),
                C("Ras Al Khaimah", "Ras əl-Xeymə", "Рас-эль-Хайма", new[] { "Ras al Khaimah", "Ras el Xeyme" }, A("RKT", "Ras Al Khaimah International Airport", "Asia/Dubai")).Info("", "An emirate between the Hajar Mountains and the Gulf, with Jebel Jais, the UAE's highest peak.", "Hacər dağları ilə Fars körfəzi arasında əmirlik; BƏƏ-nin ən hündür zirvəsi Cəbəl Cais.", "Эмират между горами Хаджар и Персидским заливом; Джебель-Джайс — высочайшая вершина ОАЭ."),
                C("Al Ain", "Əl-Ayn", "Эль-Айн", new[] { "Al Ain", "El Ayn" }, A("AAN", "Al Ain International Airport", "Asia/Dubai")).Info("", "The oasis city of the UAE, known for its date palms, forts and Jebel Hafeet.", "BƏƏ-nin vaha şəhəri: xurma bağları, qalalar və Cəbəl Hafit dağı.", "Город-оазис ОАЭ: финиковые пальмы, форты и гора Джебель-Хафит."),
            }),
        new CatalogCountrySeed("GB", "United Kingdom", "Birləşmiş Krallıq", "Великобритания",
            "Big Ben and the Thames: royal history in the heart of London.",
            "Biq Ben və Temza: Londonun mərkəzində kral tarixi.",
            "Биг-Бен и Темза: королевская история в самом сердце Лондона.",
            "uk-london.jpg", new[]
            {
                C("London", "London", "Лондон", new[] { "Londra" }, A("LHR", "Heathrow Airport", "Europe/London")).Info("uk-london.jpg", "Big Ben and the Thames: royal history in the heart of London.", "Biq Ben və Temza: Londonun mərkəzində kral tarixi.", "Биг-Бен и Темза: королевская история в самом сердце Лондона."),
                C("Manchester", "Mançester", "Манчестер", new[] { "Mancester", "Manchester" }, A("MAN", "Manchester Airport", "Europe/London")).Info("uk-manchester.jpg", "A city of Victorian architecture, led by its neo-Gothic Town Hall.", "Viktoriya dövrü memarlığı şəhəri: onun başında neoqotik Bələdiyyə binası durur.", "Город викторианской архитектуры, главное здание которого — неоготическая ратуша."),
                C("Birmingham", "Birmingem", "Бирмингем", new[] { "Birmingham" }, A("BHX", "Birmingham Airport", "Europe/London")).Info("", "England's second city, built on canals, with a modern centre and the Bullring.", "İngiltərənin ikinci şəhəri: kanallar üzərində, müasir mərkəz və Bullrinq.", "Второй город Англии на каналах с современным центром и комплексом Буллринг."),
                C("Edinburgh", "Edinburq", "Эдинбург", new[] { "Edinburgh" }, A("EDI", "Edinburgh Airport", "Europe/London")).Info("", "Scotland's capital under its castle, with the Royal Mile and the old town.", "Qala altında Şotlandiyanın paytaxtı: Kral Mili və köhnə şəhər.", "Столица Шотландии под своим замком: Королевская миля и старый город."),
                C("Glasgow", "Qlazqo", "Глазго", new[] { "Glasgow" }, A("GLA", "Glasgow Airport", "Europe/London")).Info("", "Scotland's largest city, rich in Victorian architecture, music and museums.", "Şotlandiyanın ən böyük şəhəri: Viktoriya memarlığı, musiqi və muzeylər.", "Крупнейший город Шотландии: викторианская архитектура, музыка и музеи."),
                C("Bristol", "Bristol", "Бристоль", new[] { "Bristol" }, A("BRS", "Bristol Airport", "Europe/London")).Info("", "A harbour city of hot-air balloons and the Clifton Suspension Bridge.", "İsti hava şarları və Klifton asma körpüsü ilə liman şəhəri.", "Портовый город воздушных шаров и Клифтонского подвесного моста."),
            }),
        new CatalogCountrySeed("FR", "France", "Fransa", "Франция",
            "The Eiffel Tower glowing over the Seine at dusk, and the sunny Riviera beyond.",
            "Qürub çağı Sena çayı üzərində parlayan Eyfel qülləsi və uzaqdakı Günəşli Rivyera.",
            "Эйфелева башня, сияющая над Сеной в сумерках, и солнечная Ривьера за ней.",
            "france-paris.jpg", new[]
            {
                C("Paris", "Paris", "Париж", None, A("CDG", "Paris Charles de Gaulle Airport", "Europe/Paris")).Info("france-paris.jpg", "The Eiffel Tower glowing over the Seine at dusk.", "Qürub çağı Sena çayı üzərində parlayan Eyfel qülləsi.", "Эйфелева башня, сияющая над Сеной в сумерках."),
                C("Nice", "Nitsa", "Ницца", new[] { "Nice", "Nis", "Ниццы", "Ниццу" }, A("NCE", "Nice Côte d'Azur Airport", "Europe/Paris")).Info("france-nice.jpg", "The Bay of Angels and the seafront of the French Riviera.", "Mələklər körfəzi və Fransız Rivyerasının sahil xətti.", "Ангельская бухта и набережная Французской Ривьеры."),
                C("Lyon", "Lion", "Лион", new[] { "Lyon" }, A("LYS", "Lyon-Saint-Exupery Airport", "Europe/Paris")).Info("", "A city of two rivers and Renaissance lanes, the capital of French gastronomy.", "İki çay və İntibah dövrü küçələri şəhəri; Fransa qastronomiyasının paytaxtı.", "Город двух рек и улочек эпохи Возрождения, столица французской гастрономии."),
                C("Marseille", "Marsel", "Марсель", new[] { "Marseille", "Marseilles" }, A("MRS", "Marseille Provence Airport", "Europe/Paris")).Info("", "France's oldest port, with the Vieux-Port and Notre-Dame de la Garde above it.", "Fransanın ən qədim limanı: Vyö-Por və üzərində Notr-Dam-de-la-Qard bazilikası.", "Старейший порт Франции: Старая гавань и базилика Нотр-Дам-де-ла-Гард над ней."),
                C("Toulouse", "Tuluza", "Тулуза", new[] { "Toulouse" }, A("TLS", "Toulouse-Blagnac Airport", "Europe/Paris")).Info("", "The Pink City of red brick on the Garonne, and Europe's aerospace capital.", "Qaronna sahilində qırmızı kərpicdən Çəhrayı şəhər və Avropanın aerokosmik paytaxtı.", "Розовый город из красного кирпича на Гаронне, аэрокосмическая столица Европы."),
                C("Bordeaux", "Bordo", "Бордо", new[] { "Bordeaux" }, A("BOD", "Bordeaux-Merignac Airport", "Europe/Paris")).Info("", "A wine capital on the Garonne, with elegant 18th-century quays and the Place de la Bourse.", "Qaronna sahilində şərab paytaxtı: zərif XVIII əsr sahilləri və Birja meydanı.", "Винная столица на Гаронне: изящные набережные XVIII века и площадь Биржи."),
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
                C("Hamburg", "Hamburq", "Гамбург", new[] { "Hamburg" }, A("HAM", "Hamburg Airport", "Europe/Berlin")).Info("", "A port city of canals and warehouses, with the Elbphilharmonie above the harbour.", "Kanallar və anbar binaları şəhəri; liman üzərində Elbfilarmoniya konsert salonu.", "Портовый город каналов и складов с Эльбской филармонией над гаванью."),
                C("Dusseldorf", "Düsseldorf", "Дюссельдорф", new[] { "Dusseldorf", "Düsseldorf", "Dyusseldorf" }, A("DUS", "Dusseldorf Airport", "Europe/Berlin")).Info("", "A Rhine city of fashion and art, with a lively old town and the Rheinturm.", "Reyn sahilində moda və incəsənət şəhəri; canlı köhnə şəhər və Reyn qülləsi.", "Рейнский город моды и искусства с оживлённым старым городом и Рейнской башней."),
                C("Cologne", "Köln", "Кёльн", new[] { "Koln", "Köln", "Keln" }, A("CGN", "Cologne Bonn Airport", "Europe/Berlin")).Info("", "The Gothic cathedral towers over the Rhine in this old Roman city.", "Qədim Roma şəhərində Reyn üzərində ucalan Qotik kafedral.", "Готический собор возвышается над Рейном в древнем римском городе."),
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
                C("Naples", "Neapol", "Неаполь", new[] { "Napoli", "Naples" }, A("NAP", "Naples International Airport", "Europe/Rome")).Info("", "A lively bay city beneath Vesuvius, the birthplace of pizza.", "Vezuvi altında canlı körfəz şəhəri, pitsanın vətəni.", "Оживлённый город на заливе под Везувием, родина пиццы."),
                C("Bologna", "Bolonya", "Болонья", new[] { "Bologna" }, A("BLQ", "Bologna Guglielmo Marconi Airport", "Europe/Rome")).Info("", "A medieval city of red porticoes and towers, home of Europe's oldest university.", "Qırmızı portiklər və qüllələr şəhəri, Avropanın ən qədim universitetinin vətəni.", "Средневековый город красных портиков и башен, родина старейшего университета Европы."),
                C("Florence", "Florensiya", "Флоренция", new[] { "Firenze", "Florence" }, A("FLR", "Florence Peretola Airport", "Europe/Rome")).Info("", "The cradle of the Renaissance, with the Duomo and the Ponte Vecchio on the Arno.", "İntibahın beşiyi: Arno üzərində Duomo və Ponte Vekkio.", "Колыбель Возрождения: Дуомо и мост Понте-Веккьо на Арно."),
            }),
        new CatalogCountrySeed("ES", "Spain", "İspaniya", "Испания",
            "Gaudí's Sagrada Família towers over Barcelona; Madrid keeps the capital's pace.",
            "Qaudinin Sagrada Familiyası Barselonanın üzərində ucalır; Madrid paytaxt ritmini saxlayır.",
            "Саграда Фамилия Гауди возвышается над Барселоной; Мадрид живёт в ритме столицы.",
            "spain-madrid.jpg", new[]
            {
                C("Madrid", "Madrid", "Мадрид", None, A("MAD", "Adolfo Suárez Madrid–Barajas Airport", "Europe/Madrid")).Info("spain-madrid.jpg", "The Puerta de Alcalá, a neoclassical gate in the centre of Spain's capital.", "Puerta de Alkala: İspaniya paytaxtının mərkəzindəki neoklassik qapı.", "Ворота Алькала — неоклассические ворота в центре столицы Испании."),
                C("Barcelona", "Barselona", "Барселона", new[] { "Barcelona", "Барселоны", "Барселону" }, A("BCN", "Josep Tarradellas Barcelona–El Prat Airport", "Europe/Madrid")).Info("spain-barcelona.jpg", "Gaudí's Sagrada Família towers over the city.", "Qaudinin Sagrada Familiyası şəhərin üzərində ucalır.", "Саграда Фамилия Гауди возвышается над городом."),
                C("Valencia", "Valensiya", "Валенсия", new[] { "Valencia" }, A("VLC", "Valencia Airport", "Europe/Madrid")).Info("", "A sunny city of the City of Arts and Sciences, with a long beach and paella.", "Günəşli şəhər: İncəsənət və Elmlər Şəhəri, uzun çimərlik və paelya.", "Солнечный город с Городом искусств и наук, длинным пляжем и паэльей."),
                C("Malaga", "Malaqa", "Малага", new[] { "Malaga", "Málaga" }, A("AGP", "Malaga-Costa del Sol Airport", "Europe/Madrid")).Info("", "A Costa del Sol port beneath the Alcazaba, and the birthplace of Picasso.", "Alkasaba altında Kosta del Sol limanı və Pikassonun doğulduğu şəhər.", "Порт Коста-дель-Соль под Алькасабой и родина Пикассо."),
                C("Seville", "Sevilya", "Севилья", new[] { "Sevilla", "Seville" }, A("SVQ", "Seville Airport", "Europe/Madrid")).Info("", "The capital of Andalusia, with the Alcazar, the Giralda and flamenco.", "Əndəlusun paytaxtı: Alkasar, Xiralda və flamenko.", "Столица Андалусии: Алькасар, Хиральда и фламенко."),
                C("Palma de Mallorca", "Palma-de-Malyorka", "Пальма-де-Майорка", new[] { "Palma", "Mallorca" }, A("PMI", "Palma de Mallorca Airport", "Europe/Madrid")).Info("", "The Mallorcan capital, with a Gothic cathedral on the bay.", "Mayorka adasının paytaxtı: körfəz sahilində Qotik kafedral.", "Столица Майорки с готическим собором на берегу бухты."),
            }),
        new CatalogCountrySeed("GE", "Georgia", "Gürcüstan", "Грузия",
            "Tbilisi's old town climbs the hill below Narikala, with wooden balconies and sulphur baths.",
            "Tbilisinin köhnə şəhəri Narikala qalasının altında dırmaşır: taxta eyvanlar və kükürdlü hamamlar.",
            "Старый Тбилиси поднимается по склону под Нарикалой: деревянные балконы и серные бани.",
            "georgia-tbilisi.jpg", new[]
            {
                C("Tbilisi", "Tbilisi", "Тбилиси", new[] { "Tiflis" }, A("TBS", "Tbilisi International Airport", "Asia/Tbilisi")).Info("georgia-tbilisi.jpg", "The old town climbs the hill below Narikala, beside the Kura River.", "Köhnə şəhər Kür çayının kənarında Narikala qalasının altındakı yamacla yüksəlir.", "Старый город поднимается по склону под Нарикалой, рядом с рекой Кура."),
                C("Batumi", "Batumi", "Батуми", None, A("BUS", "Batumi International Airport", "Asia/Tbilisi")).Info("georgia-batumi.jpg", "A Black Sea port city, with the Alphabetic Tower on its seafront.", "Qara dəniz liman şəhəri: sahildə Əlifba qülləsi ucalır.", "Портовый город на Чёрном море с Алфавитной башней на набережной."),
                C("Kutaisi", "Kutaisi", "Кутаиси", new[] { "Кутаиси" }, A("KUT", "Kutaisi International Airport", "Asia/Tbilisi")).Info("", "One of Georgia's oldest cities, home to the Bagrati Cathedral and the Gelati Monastery.", "Gürcüstanın ən qədim şəhərlərindən biri: Baqrati kafedralı və Gelati monastırı.", "Один из древнейших городов Грузии: собор Баграти и монастырь Гелати."),
            }),
        new CatalogCountrySeed("NL", "Netherlands", "Niderland", "Нидерланды",
            "Amsterdam's canal houses leaning over the water, built in the Golden Age.",
            "Qızıl dövrdə tikilmiş, suyun üzərinə əyilən Amsterdam kanal evləri.",
            "Амстердамские дома на каналах, склонившиеся над водой, построены в Золотой век.",
            "netherlands-amsterdam.jpg", new[]
            {
                C("Amsterdam", "Amsterdam", "Амстердам", None, A("AMS", "Amsterdam Airport Schiphol", "Europe/Amsterdam")).Info("netherlands-amsterdam.jpg", "Canal houses of the Golden Age leaning over the water.", "Qızıl dövrdə tikilmiş, suyun üzərinə əyilən kanal evləri.", "Дома Золотого века на каналах, склонившиеся над водой."),
                C("Rotterdam", "Rotterdam", "Роттердам", new[] { "Rotterdam" }, A("RTM", "Rotterdam The Hague Airport", "Europe/Amsterdam")).Info("", "A bold modern port city, with the Erasmus Bridge and the Markthal.", "Cəsarətli müasir liman şəhəri: Erazm körpüsü və Markthal.", "Смелый современный портовый город: мост Эразма и Маркталь."),
                C("Eindhoven", "Eyndhoven", "Эйндховен", new[] { "Eindhoven" }, A("EIN", "Eindhoven Airport", "Europe/Amsterdam")).Info("", "A design and technology city in the south of the Netherlands.", "Niderlandın cənubunda dizayn və texnologiya şəhəri.", "Город дизайна и технологий на юге Нидерландов."),
                C("Maastricht", "Maastrixt", "Маастрихт", new[] { "Maastricht" }, A("MST", "Maastricht Aachen Airport", "Europe/Amsterdam")).Info("", "A Roman-founded city on the Maas with cobbled squares and cafés.", "Maas çayı sahilində Roma dövründə qurulan şəhər: daş döşəli meydanlar və kafelər.", "Город римской основы на Мёзе с мощёными площадями и кафе."),
                C("Groningen", "Qroningen", "Гронинген", new[] { "Groningen" }, A("GRQ", "Groningen Airport Eelde", "Europe/Amsterdam")).Info("", "A student city in the north, with a lively centre and the Martinitoren.", "Şimalda tələbə şəhəri: canlı mərkəz və Martini qülləsi.", "Студенческий город на севере с оживлённым центром и башней Мартини."),
            }),
        new CatalogCountrySeed("JP", "Japan", "Yaponiya", "Япония",
            "Tokyo's sprawling skyline, with Mount Fuji on the horizon on a clear day.",
            "Tokionun geniş silueti; açıq havada üfüqdə Fuji dağı görünür.",
            "Бескрайний силуэт Токио, а в ясный день на горизонте виднеется гора Фудзи.",
            "japan-tokyo.jpg", new[]
            {
                C("Tokyo", "Tokio", "Токио", new[] { "Tokyo", "Tokio" }, A("HND", "Tokyo Haneda Airport", "Asia/Tokyo"), A("NRT", "Narita International Airport", "Asia/Tokyo")).Info("japan-tokyo.jpg", "Tokyo's sprawling skyline, with Mount Fuji on the horizon on a clear day.", "Tokionun geniş silueti; açıq havada üfüqdə Fuji dağı görünür.", "Бескрайний силуэт Токио, а в ясный день на горизонте виднеется гора Фудзи."),
                C("Osaka", "Osaka", "Осака", new[] { "Осаки", "Осаку" }, A("KIX", "Kansai International Airport", "Asia/Tokyo")).Info("japan-osaka.jpg", "Osaka Castle, with its stone walls, among the towers of the business district.", "Osaka qalası, daş divarları ilə, iş rayonunun göydələnləri arasında.", "Замок Осаки с каменными стенами среди башен делового района."),
                C("Nagoya", "Naqoya", "Нагоя", new[] { "Nagoya" }, A("NGO", "Chubu Centrair International Airport", "Asia/Tokyo")).Info("", "A castle city and industrial hub, with the golden dolphins of Nagoya Castle.", "Qala şəhəri və sənaye mərkəzi; Nagoya qalasının qızıl delfinləri.", "Город-замок и промышленный центр с золотыми дельфинами замка Нагоя."),
                C("Fukuoka", "Fukuoka", "Фукуока", new[] { "Fukuoka" }, A("FUK", "Fukuoka Airport", "Asia/Tokyo")).Info("", "A relaxed port city known for its ramen stalls and nearby beaches.", "Ramen köşkləri və yaxın çimərlikləri ilə tanınan sakit liman şəhəri.", "Спокойный портовый город, известный лапшой рамэн и ближними пляжами."),
                C("Sapporo", "Sapporo", "Саппоро", new[] { "Sapporo" }, A("CTS", "New Chitose Airport", "Asia/Tokyo")).Info("", "The snowy capital of Hokkaido, famous for its winter festival and beer.", "Hokkaydonun qarlı paytaxtı, qış festivalı və pivəsi ilə məşhurdur.", "Заснеженная столица Хоккайдо, известная зимним фестивалем и пивом."),
            }),
        new CatalogCountrySeed("MV", "Maldives", "Maldiv", "Мальдивы",
            "Overwater villas on a turquoise lagoon, reached by a wooden jetty.",
            "Firuzəyi laqunda taxta körpü ilə çatılan su üstü villalar.",
            "Виллы над водой на бирюзовой лагуне, к которым ведёт деревянный причал.",
            "maldives-male.jpg", new[]
            {
                C("Malé", "Male", "Мале", new[] { "Male", "Male City" }, A("MLE", "Velana International Airport", "Indian/Maldives")).Info("maldives-male.jpg", "The gateway to the atolls: overwater villas on a turquoise lagoon.", "Atollara qapı: firuzəyi laqunda su üstü villalar.", "Врата к атоллам: виллы над водой на бирюзовой лагуне.")
            }),
        new CatalogCountrySeed("PL", "Poland", "Polşa", "Польша",
            "Warsaw's rebuilt Old Town, a UNESCO site, and the Vistula flowing through the capital.",
            "Varşavanın yenidən qurulmuş Köhnə şəhəri (YUNESKO) və paytaxtdan keçən Vistula çayı.",
            "Восстановленный Старый город Варшавы (ЮНЕСКО) и Висла, текущая через столицу.",
            "hero-aircraft.jpg", new[]
            {
                C("Warsaw", "Varşava", "Варшава", new[] { "Warszawa", "Varsava" }, A("WAW", "Warsaw Chopin Airport", "Europe/Warsaw")).Info("hero-aircraft.jpg", "A capital rebuilt from the ashes, with a UNESCO Old Town and the Palace of Culture.", "Küllərdən yenidən qurulan paytaxt: YUNESKO Köhnə şəhəri və Mədəniyyət Sarayı.", "Столица, восставшая из пепла: Старый город ЮНЕСКО и Дворец культуры."),
                C("Krakow", "Krakov", "Краков", new[] { "Kraków", "Krakow" }, A("KRK", "Krakow John Paul II Airport", "Europe/Warsaw")).Info("", "A medieval royal city with the Main Market Square and Wawel Castle.", "Orta əsr kral şəhəri: Əsas bazar meydanı və Vavel qalası.", "Средневековый королевский город: Рыночная площадь и Вавельский замок."),
                C("Gdansk", "Qdansk", "Гданьск", new[] { "Gdańsk", "Gdansk" }, A("GDN", "Gdansk Lech Walesa Airport", "Europe/Warsaw")).Info("", "A Baltic port of colourful merchant houses on the Motlawa river.", "Motlava çayı sahilində rəngarəng tacir evləri olan Baltik limanı.", "Балтийский порт с разноцветными купеческими домами на реке Мотлава."),
                C("Wroclaw", "Vrotslav", "Вроцлав", new[] { "Wrocław", "Wroclaw" }, A("WRO", "Wroclaw Copernicus Airport", "Europe/Warsaw")).Info("", "A city of bridges and islands on the Oder, with a vivid market square.", "Odra çayında körpülər və adalar şəhəri, rəngarəng bazar meydanı ilə.", "Город мостов и островов на Одре с красочной рыночной площадью."),
                C("Katowice", "Katovitse", "Катовице", new[] { "Katowice" }, A("KTW", "Katowice Airport", "Europe/Warsaw")).Info("", "A former mining city turned cultural hub, with the Spodek arena and modern concert halls.", "Keçmiş mədən şəhəri, indi mədəniyyət mərkəzi: Spodek arenası və müasir konsert salonları.", "Бывший шахтёрский город, ставший культурным центром: арена «Сподек» и современные концертные залы."),
            }),
        new CatalogCountrySeed("AT", "Austria", "Avstriya", "Австрия",
            "Vienna's imperial palaces and coffee houses, with the Alps beyond.",
            "Vyananın imperiya sarayları və qəhvəxanaları, arxada isə Alp dağları.",
            "Императорские дворцы и кофейни Вены, а за ними Альпы.",
            "hero-aircraft.jpg", new[]
            {
                C("Vienna", "Vyana", "Вена", new[] { "Wien", "Vienna" }, A("VIE", "Vienna International Airport", "Europe/Vienna")).Info("hero-aircraft.jpg", "The imperial capital of palaces, opera and coffee houses on the Danube.", "Dunay sahilində saraylar, opera və qəhvəxanalar olan imperiya paytaxtı.", "Императорская столица дворцов, оперы и кофеен на Дунае."),
                C("Salzburg", "Zalsburq", "Зальцбург", new[] { "Salzburg" }, A("SZG", "Salzburg Airport", "Europe/Vienna")).Info("", "Mozart's birthplace beneath its fortress, with the Alps just beyond.", "Qala altında Motsartın doğulduğu şəhər, yaxınlıqda Alp dağları.", "Родина Моцарта под своей крепостью, а рядом — Альпы."),
                C("Graz", "Qrats", "Грац", new[] { "Graz" }, A("GRZ", "Graz Airport", "Europe/Vienna")).Info("", "A UNESCO old town with a clock tower on the Schlossberg and a modern art museum.", "Şlossberqdə saat qülləsi və müasir incəsənət muzeyi olan YUNESKO köhnə şəhəri.", "Старый город ЮНЕСКО с часовой башней на Шлоссберге и музеем современного искусства."),
                C("Innsbruck", "İnsbruk", "Инсбрук", new[] { "Innsbruck" }, A("INN", "Innsbruck Airport", "Europe/Vienna")).Info("", "An Alpine city ringed by mountains, with the Golden Roof in its old town.", "Dağlarla əhatə olunmuş Alp şəhəri; köhnə şəhərdə Qızıl dam.", "Альпийский город среди гор с Золотой крышей в старом городе."),
                C("Linz", "Lints", "Линц", new[] { "Linz" }, A("LNZ", "Linz Airport", "Europe/Vienna")).Info("", "A Danube city of industry and arts, home of the Ars Electronica Center.", "Dunay sahilində sənaye və incəsənət şəhəri, Ars Elektronika mərkəzi ilə.", "Дунайский город индустрии и искусства с центром Ars Electronica."),
            }),
        new CatalogCountrySeed("KZ", "Kazakhstan", "Qazaxıstan", "Казахстан",
            "The Baiterek tower in Astana and the snow-capped Tian Shan above Almaty.",
            "Astanada Bayterek qülləsi və Almatı üzərində qarlı Tyan-Şan.",
            "Башня Байтерек в Астане и заснеженный Тянь-Шань над Алматы.",
            "hero-aircraft.jpg", new[]
            {
                C("Almaty", "Almatı", "Алматы", new[] { "Alma-Ata", "Almati" }, A("ALA", "Almaty International Airport", "Asia/Aqtau")).Info("hero-aircraft.jpg", "A city beneath the snowy Tian Shan, with green parks and mountain cable cars.", "Qarlı Tyan-Şan altında şəhər: yaşıl parklar və dağ kanat yolları.", "Город под заснеженным Тянь-Шанем с зелёными парками и канатными дорогами."),
                C("Astana", "Astana", "Астана", new[] { "Nur-Sultan", "Nursultan" }, A("NQZ", "Astana Nursultan Nazarbayev International Airport", "Asia/Aqtau")).Info("", "The futuristic capital, with the Baiterek tower and the Khan Shatyr.", "Fütüristik paytaxt: Bayterek qülləsi və Xan Şatır.", "Футуристичная столица: башня Байтерек и Хан-Шатыр."),
                C("Shymkent", "Şımkent", "Шымкент", new[] { "Chimkent", "Shimkent" }, A("CIT", "Shymkent International Airport", "Asia/Aqtau")).Info("", "A southern city of bazaars, parks and Silk Road history.", "Cənub şəhəri: bazarlar, parklar və İpək yolu tarixi.", "Южный город базаров, парков и истории Шёлкового пути."),
                C("Aktau", "Aktau", "Актау", new[] { "Aktau" }, A("SCO", "Aktau International Airport", "Asia/Aqtau")).Info("", "A Caspian port city of desert cliffs and the sea.", "Xəzər sahilində liman şəhəri: səhra qayaları və dəniz.", "Каспийский портовый город пустынных скал и моря."),
                C("Atyrau", "Atırau", "Атырау", new[] { "Atyrau" }, A("GUW", "Atyrau International Airport", "Asia/Atyrau")).Info("", "An oil city on the Ural river, where Europe meets Asia.", "Ural çayında neft şəhəri, Avropanın Asiya ilə qovuşduğu yer.", "Нефтяной город на реке Урал, где Европа встречается с Азией."),
            }),
        new CatalogCountrySeed("UZ", "Uzbekistan", "Özbəkistan", "Узбекистан",
            "The turquoise domes of the Registan in Samarkand, a jewel of the Silk Road.",
            "Səmərqənddə Reqistanın firuzəyi günbəzləri, İpək yolunun incisi.",
            "Бирюзовые купола Регистана в Самарканде — жемчужина Шёлкового пути.",
            "hero-aircraft.jpg", new[]
            {
                C("Tashkent", "Daşkənd", "Ташкент", new[] { "Tashkent", "Toshkent" }, A("TAS", "Tashkent International Airport", "Asia/Tashkent")).Info("hero-aircraft.jpg", "A green capital of wide avenues, metro stations like palaces and old bazaars.", "Geniş prospektlər, saray kimi metro stansiyaları və qədim bazarlar şəhəri.", "Зелёная столица широких проспектов, станций метро-дворцов и старых базаров."),
                C("Samarkand", "Səmərqənd", "Самарканд", new[] { "Samarqand", "Samarkand" }, A("SKD", "Samarkand International Airport", "Asia/Samarkand")).Info("", "The Registan's tiled madrasas, one of the great cities of the Silk Road.", "Reqistanın kaşılı mədrəsələri, İpək yolunun böyük şəhərlərindən biri.", "Облицованные медресе Регистана, один из великих городов Шёлкового пути."),
                C("Bukhara", "Buxara", "Бухара", new[] { "Bukhara", "Buxoro" }, A("BHK", "Bukhara International Airport", "Asia/Samarkand")).Info("", "A living museum of mosques, madrasas and trading domes in its old town.", "Köhnə şəhərdə məscidlər, mədrəsələr və ticarət günbəzləri ilə yaşayan muzey.", "Живой музей мечетей, медресе и торговых куполов в старом городе."),
                C("Urgench", "Urgenç", "Ургенч", new[] { "Urgench", "Urganch" }, A("UGC", "Urgench International Airport", "Asia/Samarkand")).Info("", "The gateway to Khiva and its walled old city of clay minarets.", "Xivə və onun divarlı köhnə şəhərinin gil minarələrinə qapı.", "Ворота в Хиву с её глиняными минаретами внутри крепостных стен."),
                C("Fergana", "Fərqanə", "Фергана", new[] { "Fergana", "Farg'ona" }, A("FEG", "Fergana International Airport", "Asia/Tashkent")).Info("", "A fertile valley city known for silk, ceramics and orchards.", "İpək, keramika və meyvə bağları ilə tanınan bərəkətli vadi şəhəri.", "Город плодородной долины, известный шёлком, керамикой и садами."),
            })
    };

    /// <summary>
    /// Countries that were in the catalog before the network was narrowed to the sixteen above. They are switched off (IsActive = false) with their
    /// cities and airports and nothing is deleted, so bookings, payments and every relation stay intact. Switching one on again in the admin panel
    /// is not undone by the seed. An airport that has an active route is left on.
    /// </summary>
    public static readonly IReadOnlyList<string> RetiredCountryCodes = new[] { "QA", "SA", "CH", "US", "CA", "CN", "SG", "TH" };
}
