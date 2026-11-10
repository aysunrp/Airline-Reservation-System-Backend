# Yeni kompüterdə quraşdırma (clone-dan sonra)

Bu sənəd AEROVA-nı GitHub-dan clone etdikdən sonra başqa kompüterdə işə salmaq üçündür. Heç bir gizli açar Git-də saxlanılmır, onları hər kompüterdə özünüz təyin edirsiniz.

## Nə lazımdır
- .NET 8 SDK (`dotnet --version`) və `dotnet-ef` aləti: `dotnet tool install --global dotnet-ef`
- SQL Server Express, nümunə adı **`.\SQLEXPRESS`** (connection string: `appsettings.json` → `ConnectionStrings:DefaultConnection`; başqa ad olsa orada dəyişin)
- İki repo: bu backend və ayrıca **Airline-Reservation-System-Frontend** (aşağıdakı 5-ci bəndə baxın)

## 1. Verilənlər bazası
Boş baza ilə başlamaq üçün (yedək lazım deyil, baza yenidir):

```powershell
dotnet ef database update --project Repository --startup-project AirlineReservationSystem
```

API ilk dəfə işə düşəndə şəhər kataloqu, sayt mətnləri və hero video yazıları avtomatik doldurulur. Demo uçuşlar üçün `Seed:Enabled` və ya admin hesabı üçün `Bootstrap` açarları ayrıca açılır (`superadmin-setup.md`).

Başqa kompüterdən bacpac gətirmisinizsə, miqrasiyanın əvəzinə SQL Server Management Studio-da **Import Data-tier Application** ilə `AerovaDb` adı ilə idxal edin. Bacpac real məlumat daşıyır, onu açıq repo-ya qoymayın.

## 2. Məcburi gizli açar: `Jwt:Secret`
Bu açar olmadan API işə düşmür (`Jwt:Secret is missing or shorter than 32 bytes`). Layihə qovluğunda (`AirlineReservationSystem/AirlineReservationSystem`):

```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
dotnet user-secrets set "Jwt:Secret" ([Convert]::ToBase64String($b))
```

Hər kompüter üçün ayrı açar yaradın. Açarı dəyişmək mövcud giriş sessiyalarını etibarsız edir.

## 3. İstəyə bağlı açarlar (yoxdursa yalnız həmin funksiya işləmir)
| Funksiya | Açarlar | Sənəd |
| --- | --- | --- |
| E-poçt (təsdiq, şifrə bərpası, ləğv linki) | `Smtp:UserName`, `Smtp:SenderEmail`, `Smtp:Password` | `email-setup.md` |
| Stripe ödənişi (test rejimi) | `Stripe:SecretKey` (`sk_test_…`), `Stripe:WebhookSecret` | `stripe-setup.md` |
| AI səyahət köməkçisi | `TravelAssistant:ApiKey` | `travel-assistant-setup.md` |
| İlk super admin | `Bootstrap:Enabled`, `Bootstrap:SuperAdminEmail`, `Bootstrap:SuperAdminPassword` | `superadmin-setup.md` |

Mühit dəyişəni kimi eyni açarlar `Bölmə__Açar` formasındadır, məsələn `Jwt__Secret`.

## 4. API-ni işə salmaq
```powershell
dotnet run --project AirlineReservationSystem --launch-profile http
```
Ünvan: `http://localhost:5080` (Swagger: `/swagger`). `Development` rejimində CORS yalnız `http://localhost:5500` və `http://127.0.0.1:5500` üçün açıqdır (`appsettings.Development.json` → `Cors:AllowedOrigins`). Frontend başqa portda açılacaqsa, orada həmin ünvanı əlavə edin.

`Media/` qovluğu (yüklənmiş şəkillər və hero video) repo ilə gəlir və API tərəfindən `/media/…` ünvanından verilir.

## 5. Frontend
Ayrı repo-dur, quraşdırma `README.md`-dədir. Qısası: repo **kökü** `http://localhost:5500` ünvanından açılmalıdır.

## E-poçt və Stripe linkləri
Backend e-poçt və Stripe qayıdış linklərini `Auth:FrontendBaseUrl` (`appsettings.json`, standart `http://localhost:5500`) əsasında qurur. Ödəniş frontend-in açıldığı ünvandan başladısa (`localhost` və ya `127.0.0.1`, eyni port), qayıdış həmin ünvana olur. Frontend başqa portda işləyirsə `Auth:FrontendBaseUrl` dəyərini onunla eyniləşdirin.
