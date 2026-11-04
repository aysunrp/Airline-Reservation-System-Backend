namespace Service.Helpers.Seeding;

public static partial class SiteContentData
{
    public const string HomePage = "home";
    public const string FooterPage = "footer";
    public const string SupportPage = "support";

    /// <summary>Keys are full <c>data-i18n</c> paths (e.g. <c>home.heroTitle1</c>).</summary>
    public static readonly IReadOnlyList<Item> Home = new List<Item>
    {
        new("home.heroEyebrow", "Lüks Səyahət Təcrübəsi"),
        new("home.heroTitle1", "Səyahətiniz"),
        new("home.heroTitle2", "Buradan Başlayır"),
        new("home.heroDescription", "Yeni istiqamətlər kəşf edin, unudulmaz xatirələr yaradın və gözləntilərinizin fövqünə uçun."),
        new("home.bookNow", "İndi bron et"),
        new("home.searchTitle", "Haraya uçmaq istəyirsiniz?"),
        new("home.searchPlaceholder", "Məsələn: Bakıdan Londona ən ucuz uçuşu tap..."),
        new("home.searchFlights", "Uçuş axtar"),
        new("home.heroAria", "Buludların üzərində uçan təyyarə"),
        new("home.voiceStart", "Səsli axtarışı başlat"),
        new("ui.exploreTheWorld", "Dünyanı kəşf et"),
        new("ui.discoverYourNextDestination", "Növbəti istiqamətinizi kəşf edin"),
        new("home.destinationsDesc", "AEROVA ilə unudulmaz istiqamətləri kəşf edin."),
        new("home.premiumEyebrow", "Aerova Təcrübəsi"),
        new("ui.travelBetterWithAerova", "AEROVA ilə daha yaxşı səyahət edin"),
        new("ui.aerovaCombinesEffortlessBookingFlexibleTravel", "AEROVA asan bron, çevik idarəetmə və premium xidməti birləşdirir."),
        new("ui.easyBooking", "Asan bron"),
        new("ui.findAndBookYourNextFlight", "Növbəti uçuşunuzu bir neçə addımda tapın və bron edin."),
        new("destinations.bookFlight", "Uçuş bron et"),
        new("ui.smartSeatSelection", "Ağıllı oturacaq seçimi"),
        new("ui.chooseYourPreferredSeatBeforeYou", "Səyahətdən əvvəl üstünlük verdiğiniz oturacağı seçin."),
        new("home.chooseSeat", "Oturacaq seçin"),
        new("ui.manageYourTrips", "Səyahətlərinizi idarə edin"),
        new("ui.viewManageAndTrackAllYour", "Bütün gələcək səyahətlərinizi görün, idarə edin və izləyin."),
        new("nav.myTrips", "Səyahətlərim"),
        new("ui.flightStatus", "Uçuş statusu"),
        new("ui.checkTheLatestStatusOfYour", "AEROVA uçuşunuzun son statusunu yoxlayın."),
        new("home.whyPremiumTitle", "Premium təcrübə"),
        new("home.whyPremiumText", "Sizin ətrafınızda dizayn edilmiş zərif səyahət təcrübəsini kəşf edin."),
        new("home.exploreExperience", "Təcrübəni kəşf et"),
        new("ui.247Support", "24/7 dəstək"),
        new("ui.getAssistanceWheneverYouNeedIt", "Səyahətdən əvvəl və ya zamanı lazım olanda dəstək alın."),
        new("home.getSupport", "Dəstək al"),
        new("home.premiumTitle", "Öz sinfinizdə səyahət edin"),
        new("home.premiumDesc", "Zərif rahatlıqdan yüksək xidmətə qədər AEROVA ilə səyahət tərzinizi kəşf edin."),
        new("home.economyTitle", "Ekonom sinif"),
        new("home.economyDesc", "Düşünülmüş rahatlıq, əyləncə və rahat səyahət üçün lazım olan hər şey."),
        new("home.exploreEconomy", "Ekonomu kəşf et"),
        new("common.comfort", "Komfort"),
        new("home.comfortDesc", "Əlavə yer, düşünülmüş rahatlıq və daha sakit səyahət."),
        new("home.discoverComfort", "Komfortu kəşf et"),
        new("home.businessTitle", "Biznes sinif"),
        new("home.businessDesc", "Geniş oturacaqlar, zərif yeməklər və daha rahat səyahət."),
        new("home.exploreBusiness", "Biznesi kəşf et"),
        new("home.newsletterEyebrow", "Əlaqədə qalın"),
        new("home.newsletterTitle", "Səyahət ilhamı, birbaşa sizə"),
        new("home.newsletterDesc", "AEROVA yenilikləri, istiqamət ilhamı və xüsusi təkliflər üçün abunə olun."),
        new("home.newsletterLabel", "E-poçt ünvanı"),
        new("home.newsletterPlaceholder", "E-poçt ünvanınızı daxil edin"),
        new("home.newsletterButton", "Abunə ol"),
    };

    public static readonly IReadOnlyList<Item> Footer = new List<Item>
    {
        new("footer.description", "Gözləntilərinizin fövqünə uçun."),
        new("footer.quickLinks", "Sürətli keçidlər"),
        new("footer.home", "Ana səhifə"),
        new("footer.flights", "Uçuşlar"),
        new("footer.destinations", "İstiqamətlər"),
        new("footer.about", "Haqqımızda"),
        new("footer.myTrips", "Səyahətlərim"),
        new("footer.support", "Dəstək"),
        new("footer.help", "Kömək mərkəzi"),
        new("footer.contact", "Əlaqə"),
        new("footer.travelInfo", "Səyahət məlumatı"),
        new("footer.faqs", "Suallar"),
        new("footer.followUs", "Bizi izləyin"),
        new("footer.copyright", "© 2026 AEROVA. Bütün hüquqlar qorunur."),
        new("footer.privacy", "Məxfilik siyasəti"),
        new("footer.terms", "Şərtlər və qaydalar"),
        new("footer.manageBooking", "Bronu idarə et"),
    };

    public static readonly IReadOnlyList<Item> Support = new List<Item>
    {
        new("support.title", "Dəstək mərkəzi"),
        new("support.subtitle", "Rezervasiyalar, ödənişlər və hesabınızla bağlı kömək alın."),
        new("support.contactTitle", "Əlaqə yolları"),
        new("support.contactNone", "Bizə forma ilə yazın: cavabı burada, Dəstək səhifənizdə veririk və yenilik olanda e-poçt göndəririk."),
        new("support.formTitle", "Dəstəyə yazın"),
        new("support.faqTitle", "Tez-tez verilən suallar"),
        new("support.faq.q1", "Rezervasiya kodumu (PNR) harada tapa bilərəm?"),
        new("support.faq.a1", "Rezervasiya kodunuz (PNR) təsdiq səhifəsində və rezervasiya e-poçtlarında göstərilir. Hesabınıza daxil olanda bütün rezervasiyalarınız \"Səyahətlərim\"də siyahılanır."),
        new("support.faq.q2", "Rezervasiyaya necə baxa və ya onu necə idarə edə bilərəm?"),
        new("support.faq.a2", "\"Səyahətlərim\" bölməsini açın və ya rezervasiya kodunuz və sərnişinin soyadı ilə \"Rezervasiyanı idarə et\" bölməsindən istifadə edin."),
        new("support.faq.q3", "Ödəniş necə işləyir?"),
        new("support.faq.a3", "Ödəniş Stripe-ın təhlükəsiz səhifəsində edilir. AEROVA kart məlumatlarınızı görmür və saxlamır. Rezervasiya yalnız Stripe ödənişi təsdiq edəndə təsdiqlənir."),
        new("support.faq.q4", "Rezervasiyanı necə ləğv edim?"),
        new("support.faq.a4", "Rezervasiya təfərrüatlarında ləğv sorğusu verin. Hesabınızın e-poçtuna təsdiq linki göndəririk və rezervasiya yalnız o linki açıb təsdiqləyəndə ləğv olunur. Mümkün haqq ləğv zamanı göstərilən tarif qaydalarına uyğundur."),
        new("support.faq.q5", "Geri ödəniş nə vaxt gələcək?"),
        new("support.faq.a5", "Geri ödəniş Stripe təsdiq etdikdən sonra ödəniş etdiyiniz karta qaytarılır. Banklar adətən təxminən 5-10 iş günü ərzində göstərir; bu təxmindir, vəd deyil. Geri ödənişin vəziyyətini rezervasiya təfərrüatlarında və bildirişlərdə izləyə bilərsiniz."),
        new("support.faq.q6", "Baqaj və ya yemək necə əlavə edim?"),
        new("support.faq.a6", "Baqaj və yemək seçimləri rezervasiya zamanı, ödənişdən əvvəl, həmin addımda göstərilən qiymətlərlə seçilir."),
        new("support.faq.q7", "Qeydiyyat (check-in) necə işləyir?"),
        new("support.faq.a7", "Uçuşdan əvvəl bildirişlərinizə xatırlatma göndəririk. Uçuş məlumatlarına \"Səyahətlərim\"də baxın, uçuşla bağlı sualınız varsa rezervasiya kodunuzla bizə yazın."),
        new("support.faq.q8", "Daxil ola bilmirəm və ya parolu unutmuşam."),
        new("support.faq.a8", "Giriş səhifəsində \"Parolu unutmusunuz?\" bölməsindən istifadə edin: ünvanınıza birdəfəlik kod göndəririk. Təsdiq e-poçtu gəlməyibsə, profilinizdən yeni link istəyə bilərsiniz."),
    };

    public static IReadOnlyList<PageSeed> GetSeedPages() => new List<PageSeed>
    {
        new(AboutPage, About),
        new(HomePage, Home),
        new(FooterPage, Footer),
        new(SupportPage, Support),
        new(SitePage, Site),
        new(DestinationsPage, Destinations),
    };

    public static int TotalSeedCount => GetSeedPages().Sum(p => p.Items.Count);
}
