namespace Service.Helpers.Seeding;

/// <summary>
/// The home page cards and the support questions as the site showed them before they were stored in the database. They are added once,
/// when the tables are empty, so the site looks the same until an admin changes something.
/// </summary>
public static class CmsListSeedData
{
    public record FaqSeed(string Question, string Answer);

    public record CardSeed(string Section, int SortOrder, string Title, string Description, string? Label, string LinkText, string LinkUrl,
        string? IconKey, string? ImagePath, string? ImageAlt);

    public static readonly IReadOnlyList<FaqSeed> Faq = new List<FaqSeed>
    {
        new("Rezervasiya kodumu (PNR) harada tapa bilərəm?", "Rezervasiya kodunuz (PNR) təsdiq səhifəsində və rezervasiya e-poçtlarında göstərilir. Hesabınıza daxil olanda bütün rezervasiyalarınız \"Səyahətlərim\"də siyahılanır."),
        new("Rezervasiyaya necə baxa və ya onu necə idarə edə bilərəm?", "\"Səyahətlərim\" bölməsini açın və ya rezervasiya kodunuz və sərnişinin soyadı ilə \"Rezervasiyanı idarə et\" bölməsindən istifadə edin."),
        new("Ödəniş necə işləyir?", "Ödəniş Stripe-ın təhlükəsiz səhifəsində edilir. AEROVA kart məlumatlarınızı görmür və saxlamır. Rezervasiya yalnız Stripe ödənişi təsdiq edəndə təsdiqlənir."),
        new("Rezervasiyanı necə ləğv edim?", "Rezervasiya təfərrüatlarında ləğv sorğusu verin. Hesabınızın e-poçtuna təsdiq linki göndəririk və rezervasiya yalnız o linki açıb təsdiqləyəndə ləğv olunur. Mümkün haqq ləğv zamanı göstərilən tarif qaydalarına uyğundur."),
        new("Geri ödəniş nə vaxt gələcək?", "Geri ödəniş Stripe təsdiq etdikdən sonra ödəniş etdiyiniz karta qaytarılır. Banklar adətən təxminən 5-10 iş günü ərzində göstərir; bu təxmindir, vəd deyil. Geri ödənişin vəziyyətini rezervasiya təfərrüatlarında və bildirişlərdə izləyə bilərsiniz."),
        new("Baqaj və ya yemək necə əlavə edim?", "Baqaj və yemək seçimləri rezervasiya zamanı, ödənişdən əvvəl, həmin addımda göstərilən qiymətlərlə seçilir."),
        new("Qeydiyyat (check-in) necə işləyir?", "Uçuşdan əvvəl bildirişlərinizə xatırlatma göndəririk. Uçuş məlumatlarına \"Səyahətlərim\"də baxın, uçuşla bağlı sualınız varsa rezervasiya kodunuzla bizə yazın."),
        new("Daxil ola bilmirəm və ya parolu unutmuşam.", "Giriş səhifəsində \"Parolu unutmusunuz?\" bölməsindən istifadə edin: ünvanınıza birdəfəlik kod göndəririk. Təsdiq e-poçtu gəlməyibsə, profilinizdən yeni link istəyə bilərsiniz."),
    };

    public static readonly IReadOnlyList<CardSeed> Cards = new List<CardSeed>
    {
        new("why", 1, "Asan bron", "Növbəti uçuşunuzu bir neçə addımda tapın və bron edin.", null, "Uçuş bron et", "booking.html", "booking", null, null),
        new("why", 2, "Ağıllı oturacaq seçimi", "Səyahətdən əvvəl üstünlük verdiğiniz oturacağı seçin.", null, "Oturacaq seçin", "seat-preview.html", "seat", null, null),
        new("why", 3, "Səyahətlərinizi idarə edin", "Bütün gələcək səyahətlərinizi görün, idarə edin və izləyin.", null, "Səyahətlərim", "my-trips.html", "trips", null, null),
        new("why", 4, "Uçuş statusu", "AEROVA uçuşunuzun son statusunu yoxlayın.", null, "Uçuş statusu", "manage-booking.html", "status", null, null),
        new("why", 5, "Premium təcrübə", "Sizin ətrafınızda dizayn edilmiş zərif səyahət təcrübəsini kəşf edin.", null, "Təcrübəni kəşf et", "about-aerova.html", "premium", null, null),
        new("why", 6, "24/7 dəstək", "Səyahətdən əvvəl və ya zamanı lazım olanda dəstək alın.", null, "Dəstək al", "support.html", "support", null, null),
        new("premium", 1, "Ekonom sinif", "Düşünülmüş rahatlıq, əyləncə və rahat səyahət üçün lazım olan hər şey.", "Ekonom sinif", "Ekonomu kəşf et", "about-aerova.html#economy", null, "assets/images/economy-class.jpg", "Ekonom sinif salonunda oturacaqlar"),
        new("premium", 2, "Komfort", "Əlavə yer, düşünülmüş rahatlıq və daha sakit səyahət.", "Komfort", "Komfortu kəşf et", "about-aerova.html#comfort", null, "assets/images/comfort.jpg", "Əlavə məkanlı komfort salonu"),
        new("premium", 3, "Biznes sinif", "Geniş oturacaqlar, zərif yeməklər və daha rahat səyahət.", "Biznes sinif", "Biznesi kəşf et", "about-aerova.html#business", null, "assets/images/business-class.jpg", "Geniş oturacaqlı biznes sinif salonu"),
    };
}
