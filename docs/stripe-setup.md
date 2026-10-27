# Stripe ödənişi: quraşdırma və yoxlama

AEROVA kart məlumatını görmür. Rezervasiya ödənişi Stripe-ın **Checkout** səhifəsində edilir və rezervasiya yalnız Stripe-dan gələn,
imzası yoxlanmış **webhook** ödənişi təsdiqləyəndən sonra `Confirmed` olur. İstifadəçinin success səhifəsinə qayıtması ödənişi sübut etmir.

Əvvəlcə **verilənlər bazası miqrasiyası** (`AddStripePayments`) tətbiq olunmalıdır (yedək aldıqdan sonra, `dotnet ef database update`).
Miqrasiyasız yeni build işləməz (`Bookings.RowVersion`, yeni `Payments` sütunları və `StripeWebhookEvents` cədvəli lazımdır).

## 1. Açarlar

Yalnız **test rejimi** açarlarını istifadə edin (Stripe Dashboard, sağ üstdə "Test mode" yanılı olsun). `sk_test_...` açarı
Dashboard → Developers → API keys bölməsindədir. Açarlar **User Secrets** və ya **mühit dəyişəni** olmalıdır, `appsettings.json`-a, Git-ə
və frontend-ə yazmayın.

| Açar | Mühit dəyişəni | Dəyər |
| --- | --- | --- |
| `Stripe:SecretKey` | `Stripe__SecretKey` | `sk_test_...` |
| `Stripe:WebhookSecret` | `Stripe__WebhookSecret` | `whsec_...` (3-cü addımdan) |
| `Stripe:Currency` (ixtiyari) | `Stripe__Currency` | standart `usd` (qiymətlər USD saxlanılır, dəyişməyin) |
| `Stripe:CheckoutMinutes` (ixtiyari) | `Stripe__CheckoutMinutes` | standart `31` (Stripe minimum 30 dəq) |
| `Auth:FrontendBaseUrl` | `Auth__FrontendBaseUrl` | frontend ünvanı, məs. `http://localhost:5500`. Success/cancel ünvanları buradan qurulur |

`AirlineReservationSystem/AirlineReservationSystem` qovluğunda (açar dəyərlərini cavabda və ya əmr tarixçəsində paylaşmayın):

```powershell
dotnet user-secrets set "Stripe:SecretKey" "<sk_test_ ilə başlayan açar>"
dotnet user-secrets set "Stripe:WebhookSecret" "<whsec_ ilə başlayan açar>"
```

**Diqqət:** iki açar da təyin olunanda Stripe rejimi açılır və köhnə saxta ödəniş (`POST /api/payments`) bağlanır. Açarlar yoxdursa
`POST /api/payments/checkout-session` cavabı `503` olur, yəni frontend-dən ödəniş mümkün deyil. Saxta ödəniş yalnız Development-də
və Stripe açarları yoxkən işləyir, amma yeni frontend artıq ona müraciət etmir.

## 2. Stripe CLI (lokal webhook üçün)

Stripe localhost-a özü sorğu göndərə bilmir. Lokal yoxlama üçün Stripe CLI quraşdırın (https://docs.stripe.com/stripe-cli) və giriş edin:

```powershell
stripe login
```

## 3. Webhook-u yönləndirmək və imza açarını almaq

API işləyirkən (məs. `http://localhost:5080`):

```powershell
stripe listen --forward-to http://localhost:5080/api/payments/webhook --events checkout.session.completed,checkout.session.async_payment_succeeded,checkout.session.async_payment_failed,checkout.session.expired
```

Əmr `Ready! Your webhook signing secret is whsec_...` yazır. Bu `whsec_...` dəyərini 1-ci addımdakı `Stripe:WebhookSecret` kimi yazın və API-ni
**yenidən başladın**. `stripe listen` hər başladılanda eyni hesab üçün adətən eyni açarı verir, amma dəyişərsə yenidən yazın.

Dashboard-da (canlı mühit üçün) eyni endpoint: Developers → Webhooks → Add endpoint, ünvan `https://<domen>/api/payments/webhook`,
yuxarıdakı 4 hadisə. Verilən imza açarı canlı mühitin `Stripe__WebhookSecret` dəyəridir.

## 4. Test ödənişi

1. Frontend-i (`http://localhost:5500`) və API-ni açın, uçuş seçib rezervasiya yaradın, ödəniş səhifəsində **Pay** düyməsini basın.
2. Stripe səhifəsində test kartı: `4242 4242 4242 4242`, istənilən gələcək tarix, istənilən CVC. Real kart yazmayın.
3. Uğurlu halda `payment-success.html` açılır və rezervasiya `Confirmed` olur. `stripe listen` pəncərəsində `checkout.session.completed -> 200` görünməlidir.

Digər test halları (Stripe test kartları):

| Hal | Kart / əməl | Gözlənilən nəticə |
| --- | --- | --- |
| Rədd edilmiş kart | `4000 0000 0000 0002` | Stripe səhifəsində xəta, rezervasiya `Pending` qalır, təkrar cəhd olur |
| Ləğv | Stripe səhifəsində "geri" | `payment-cancel.html`, heç nə tutulmur |
| Webhook təkrarı | `stripe events resend <evt_...>` | Cavab `200`, rezervasiya ikinci dəfə təsdiqlənmir, bildiriş təkrarlanmır |
| Səhv imza | `curl -X POST .../api/payments/webhook -d "{}"` | `400`, heç nə dəyişmir |

## 5. Yoxlama siyahısı

- `StripeWebhookEvents` cədvəlində hər hadisə bir dəfə və nəticə mətni ilə görünür (`Confirmed`, `Already confirmed`, `Rejected: ...`).
- `Payments` sətri `Paid`, `Bookings` sətri `Confirmed`, biletlər `Issued`, oturacaqlar `Booked`.
- "Paid while the booking was ...: refund needed" nəticəsi görünsə, ödəniş gəlib, amma rezervasiya vaxtı bitmişdi: pul **əl ilə** Stripe Dashboard-dan qaytarılmalıdır.

## Məlum məhdudiyyətlər

- **Geri ödəniş avtomatik deyil.** Stripe ilə ödənilmiş rezervasiya ləğv edilsə, pul Stripe Dashboard-dan əl ilə qaytarılır (ayrıca tapşırıq kimi təklif olunub).
- Stripe hesabınızın qeydiyyat ölkəsi dəstəklənməlidir. Azərbaycanda qeydiyyatlı biznes üçün Stripe-ın dəstəyini özünüz yoxlayın.
- Real Stripe hesabı, real webhook çatdırılması və real ödəniş AEROVA tərəfindən sınaqdan keçirilməyib. Testlər saxta Stripe ilə aparılıb.
