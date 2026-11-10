# Test auditi — AEROVA backend (2026-11-10)

Yalnız audit: heç bir test faylı, istehsal kodu, verilənlər bazası və ya migrasiya dəyişdirilməyib; commit və push edilməyib. Testlər `%TEMP%\audit-be` adlı müvəqqəti surətdə işlədilib.

## 1. Ölçülmüş vəziyyət (bu gün, təmiz surətdə)

| Layihə | Test metodu | Ümumi (Theory sətirləri daxil) | Uğursuz | Uğurlu |
|---|---:|---:|---:|---:|
| Service.Tests | 718 | 952 | 12 | 940 |
| AirlineReservationSystem.Tests (API) | 341 | 562 | 6 | 556 |

Qeyd: sizin yazdığınız '16 Service + 30 Endpoint' ilkin göstəricidən fərqlidir; bu gün ölçülən yeganə rəqəm **12 + 6 = 18**-dir. Bu sessiyada Hesab tənzimləmələri, giriş mesajları, regional şəbəkə və profil işləri ilə 100-dən çox yeni test əlavə olunub, bəzi köhnə uğursuzluqlar isə həmin işlərdə düzəldilib. Hər iki ilkin siyahı mənim əlimdə yoxdur, ona görə fərqi sübut edə bilmirəm.

### Uğursuz testlər və səbəbləri

| # | Layihə | Test | Qərar | Səbəb |
|---|---|---|---|---|
| 1 | Service | NotificationEmailTests: ANotification_IsMailedToTheAccountsOwnAddress_OnceOnly | FIX | BookingConfirmed bildirişi artıq e-poçtla göndərilmir (NotificationEmailService həmin növü 'göndərilmiş' işarələyib keçir). Test başqa növ (məs. RefundCompleted/PaymentSuccessful) istifadə etməlidir. |
| 2 | Service | NotificationEmailTests: TheContactAddressOfABooking_NeverReceivesTheMail | FIX | Eyni səbəb: gözlənilən göndərmə sayı 2, faktiki 1. |
| 3 | Service | NotificationEmailTests: WithoutMailSetup_NothingIsSent_AndNothingIsMarkedSent | FIX | Eyni səbəb. |
| 4 | Service | NotificationEmailTests: AFailedDelivery_IsTriedAFewTimes_ThenLeftAlone… | FIX | Eyni səbəb: BookingConfirmed nəzərdə tutulan təkrar cəhd yoluna düşmür. |
| 5 | Service | BookingEmailConfirmationTests (7 test) | FIX | BookingService link e-poçtunu `Task.Run` ilə arxa planda göndərir (IServiceScopeFactory ilə). Testdə scope factory verilmir, ona görə heç nə göndərilmir. Test servisi birbaşa çağırmalı və ya scope factory vermelidir. |
| 6 | Service | CheckoutServiceTests: WithoutStripeSecrets_CheckoutIsUnavailable | OBSOLETE/FIX | İstehsal qaydası dəyişib: Checkout üçün yalnız SecretKey lazımdır (CanAcceptCheckout). Test WebhookSecret=null ilə istisna gözləyir. SecretKey=null ilə yenidən yazılmalıdır. |
| 7 | API | AuthEmailEndpointTests: AVerificationLink_TellsUnknownFromAlreadyUsedOverHttp | FIX | RegisterAsync köməkçisi e-poçt qurulu olanda linki özü açır; test sonra 'ilk açılış 204' gözləyir, amma artıq 422 link_confirmed alır. |
| 8 | API | AuthEmailEndpointTests: Resend_NeedsSignIn_SendsAnotherLink_AndStopsOnceConfirmed | FIX | Eyni köməkçi səbəbi: hesab artıq təsdiqlənib, 429 əvəzinə 409 gəlir. |
| 9 | API | BookingEmailConfirmationEndpointTests (2 test) | FIX | Arxa plan link e-poçtu (yarış vəziyyəti ehtimalı) və hesabın avtomatik təsdiqi səbəbindən e-poçt tapılmır / 422. |
| 10 | API | VerifiedBookingEndpointTests: ANewAccount_IsBlocked_UntilItsLinkIsOpened… | FIX | Köməkçi hesabı təsdiqlədiyi üçün bron bloklanmır (Created). |
| 11 | API | RoleAuthorizationEndpointTests: User_IsForbidden_InTheAdminArea_AndTheAttemptIsLogged | FIX | CreateAccountAsync(User) birbaşa bazaya təsdiqsiz hesab yazır; e-poçt qurulu olduqda giriş 401 verir. |

**Nəticə:** 18 uğursuzluğun hamısı **köhnəlmiş test** və ya **test köməkçisi sürüşməsidir**; heç biri istehsal xətası kimi görünmür. Bu mənim oxuduğum kod və səbəb zəncirinə əsaslanır, hər biri üçün düzəliş edilib yenidən işlədilməyib.

## 2. Hər test layihəsinin məqsədi

- **Service.Tests** — xidmət (Service) qatının iş qaydaları. Verilənlər bazası yoxdur: əl ilə yazılmış `Fake*` repozitoriyalar, sabit saat (2026-11-01 12:00 UTC). Mock kitabxanası (Moq və s.) istifadə olunmur.
- **AirlineReservationSystem.Tests** — inteqrasiya testləri. Hər test sinfi üçün real SQL Server-də ayrıca verilənlər bazası (`AerovaDb_ApiTests_*`), bütün migrasiyalarla, HTTP üzərindən (WebApplicationFactory). Stripe, e-poçt və AI provayderi test əvəzləyiciləridir.

## 3. Fayl-fayl qərar cədvəli

### Service.Tests

| Fayl | Sahə | Metod | Sətir (theory) | Qərar / qeyd |
|---|---|---:|---:|---|
| AccountEmailTests.cs | Autentifikasiya | 13 | 20 | KEEP |
| AccountSessionTests.cs | Autentifikasiya | 3 | 29 | KEEP |
| AdminAircraftServiceTests.cs | Admin | 7 | 7 | KEEP |
| AdminBookingAndPassengerTests.cs | Booking | 8 | 8 | KEEP |
| AdminFlightServiceTests.cs | Admin | 10 | 10 | KEEP |
| AdminPromoCodeServiceTests.cs | Booking | 5 | 5 | KEEP |
| AdminRouteAndScheduleTests.cs | Admin | 14 | 14 | KEEP |
| AdminUserServiceTests.cs | Admin | 23 | 25 | KEEP |
| AirportServiceTests.cs | Uçuş / Axtarış / Cədvəl | 2 | 2 | KEEP |
| AssistantTests.cs | Dəstək / Köməkçi / Səs | 36 | 55 | KEEP |
| AuthSecurityTests.cs | Autentifikasiya | 10 | 12 | KEEP |
| AuthServiceTests.cs | Autentifikasiya | 8 | 14 | KEEP |
| BookingEmailConfirmationTests.cs | Booking | 12 | 14 | FIX (7/12): link e-poçtu artıq arxa planda (IServiceScopeFactory) göndərilir, test isə sinxron gözləyir |
| BookingLifecycleTests.cs | Booking | 6 | 6 | KEEP |
| BookingLifecycleWorkerTests.cs | Booking | 5 | 5 | KEEP |
| BookingServiceTests.cs | Booking | 19 | 19 | KEEP |
| CampaignAndAdminAlertTests.cs | Admin | 29 | 31 | KEEP |
| CancellationLinkTests.cs | Geri ödəmə / Ləğv | 10 | 12 | KEEP |
| CancellationPolicyServiceTests.cs | Geri ödəmə / Ləğv | 4 | 13 | KEEP |
| CancellationServiceTests.cs | Geri ödəmə / Ləğv | 7 | 7 | KEEP |
| CheckInReminderServiceTests.cs | Digər | 5 | 5 | KEEP |
| CheckoutServiceTests.cs | Ödəniş / Stripe / Webhook | 17 | 24 | FIX (1/17): yalnız WebhookSecret olmadan Checkout açılır (CanAcceptCheckout yalnız SecretKey-ə baxır); köhnə qayda |
| DemoNetworkDataTests.cs | Uçuş / Axtarış / Cədvəl | 15 | 27 | KEEP (kövrək): sabit aralıqlar (məs. 2800-3200 uçuş) demo məlumat dəyişəndə qırılır |
| DestinationCatalogDataTests.cs | CMS / Məzmun / Media | 10 | 10 | KEEP: kataloq məlumatının bütövlüyü |
| DevelopmentAdminSeederTests.cs | Avtorizasiya / Rollar | 4 | 10 | KEEP |
| ExtraServiceOptionsTests.cs | Booking | 2 | 2 | KEEP |
| FlightGenerationServiceTests.cs | Uçuş / Axtarış / Cədvəl | 12 | 12 | KEEP |
| FlightGenerationWorkerTests.cs | Uçuş / Axtarış / Cədvəl | 9 | 11 | KEEP |
| FlightSearchAndSeatTests.cs | Oturacaq | 16 | 18 | KEEP |
| FrontendOriginTests.cs | Infrastruktur / Kontrakt | 3 | 6 | KEEP |
| NewsletterTests.cs | Bildiriş / E-poçt / Kampaniya | 25 | 31 | KEEP |
| NotificationEmailTests.cs | Bildiriş / E-poçt / Kampaniya | 7 | 7 | FIX (4/7): test BookingConfirmed bildirişini e-poçtla göndərməyi gözləyir, amma istehsal kodu bunu bilərəkdən dayandırıb |
| NotificationServiceTests.cs | Bildiriş / E-poçt / Kampaniya | 7 | 7 | KEEP |
| NotificationTicketLinkTests.cs | Bildiriş / E-poçt / Kampaniya | 3 | 13 | KEEP |
| PassengerTypeBookingTests.cs | Booking | 10 | 10 | KEEP |
| PaymentServiceTests.cs | Ödəniş / Stripe / Webhook | 6 | 8 | KEEP |
| PricingServiceTests.cs | Booking | 7 | 16 | KEEP |
| ProfileServiceTests.cs | Autentifikasiya | 13 | 16 | KEEP |
| PromoCodeServiceTests.cs | Booking | 6 | 8 | KEEP |
| RefundEmailTests.cs | Geri ödəmə / Ləğv | 10 | 13 | KEEP |
| RefundServiceTests.cs | Geri ödəmə / Ləğv | 13 | 13 | KEEP |
| RefundWebhookTests.cs | Ödəniş / Stripe / Webhook | 10 | 10 | KEEP |
| RegionalNetworkDataTests.cs | Uçuş / Axtarış / Cədvəl | 9 | 9 | KEEP: yeni regional şəbəkə üçün saat qurşağı, qiymət, təyyarə toqquşması yoxlanışı |
| ResetCodeTests.cs | Autentifikasiya | 19 | 22 | KEEP |
| RoundTripSeatTests.cs | Oturacaq | 7 | 7 | KEEP |
| ScheduleTimeCalculatorTests.cs | Uçuş / Axtarış / Cədvəl | 11 | 11 | KEEP |
| SeatPaymentRuleTests.cs | Oturacaq | 11 | 11 | KEEP |
| SignInToBookTests.cs | Booking | 3 | 3 | KEEP |
| SiteContentTests.cs | CMS / Məzmun / Media | 27 | 36 | KEEP |
| SiteMediaReferenceServiceTests.cs | CMS / Məzmun / Media | 2 | 2 | KEEP |
| SiteMediaStorageTests.cs | CMS / Məzmun / Media | 3 | 3 | KEEP |
| SmtpCheckTests.cs | Bildiriş / E-poçt / Kampaniya | 2 | 2 | KEEP |
| StripeWebhookServiceTests.cs | Ödəniş / Stripe / Webhook | 16 | 19 | KEEP |
| SuperAdminProvisioningServiceTests.cs | Avtorizasiya / Rollar | 10 | 18 | KEEP |
| SupportRealtimeTests.cs | Dəstək / Köməkçi / Səs | 8 | 8 | KEEP |
| SupportTests.cs | Dəstək / Köməkçi / Səs | 21 | 26 | KEEP |
| TravelDocumentPolicyTests.cs | Booking | 9 | 16 | KEEP |
| VerifiedEmailBookingTests.cs | Booking | 4 | 4 | KEEP |
| VoiceSearchAzerbaijaniTests.cs | Uçuş / Axtarış / Cədvəl | 43 | 66 | KEEP: Azərbaycan dili parser testləri |
| VoiceSearchTests.cs | Uçuş / Axtarış / Cədvəl | 72 | 150 | KEEP: real parser məntiqi (150 sətir data); ən böyük fayl, lakin dəyərlidir |

### AirlineReservationSystem.Tests

| Fayl | Sahə | Metod | Sətir (theory) | Qərar / qeyd |
|---|---|---:|---:|---|
| AccountDeletionEndpointTests.cs | Autentifikasiya | 13 | 13 | KEEP |
| AccountSettingsEndpointTests.cs | Autentifikasiya | 19 | 21 | KEEP |
| AdminAirportEndpointTests.cs | Admin | 2 | 2 | KEEP |
| AdminBookingEndpointTests.cs | Booking | 3 | 3 | KEEP |
| AdminBookingOptionEndpointTests.cs | Booking | 3 | 3 | KEEP |
| AdminCancellationRuleEndpointTests.cs | Geri ödəmə / Ləğv | 2 | 2 | KEEP |
| AdminCatalogEndpointTests.cs | Admin | 6 | 6 | KEEP |
| AdminFlightEndpointTests.cs | Admin | 5 | 5 | KEEP |
| AdminFlightListTests.cs | Admin | 7 | 7 | KEEP |
| AdminGenerationEndpointTests.cs | Admin | 5 | 5 | KEEP |
| AdminMediaEndpointTests.cs | Admin | 7 | 7 | KEEP |
| AdminPromoCodeEndpointTests.cs | Booking | 2 | 2 | KEEP |
| AdminScheduleDeleteEndpointTests.cs | Admin | 1 | 1 | KEEP |
| AdminSeatMapEndpointTests.cs | Oturacaq | 3 | 3 | KEEP |
| AssistantEndpointTests.cs | Dəstək / Köməkçi / Səs | 9 | 10 | KEEP |
| AuthAndProfileEndpointTests.cs | Autentifikasiya | 7 | 7 | KEEP |
| AuthEmailEndpointTests.cs | Autentifikasiya | 11 | 11 | FIX (2/11): RegisterAsync köməkçisi linki özü açır, test isə təsdiqlənməmiş hesab gözləyir |
| AuthHardeningEndpointTests.cs | Autentifikasiya | 4 | 4 | KEEP |
| AuthenticationTests.cs | Autentifikasiya | 7 | 7 | KEEP |
| BookingEmailConfirmationEndpointTests.cs | Booking | 4 | 4 | FIX (2/4): eyni səbəb — arxa plan link e-poçtu və köməkçinin avtomatik təsdiqi |
| BookingEndpointTests.cs | Booking | 8 | 8 | KEEP |
| BookingLifecycleIntegrationTests.cs | Booking | 3 | 3 | KEEP |
| CampaignEndpointTests.cs | Bildiriş / E-poçt / Kampaniya | 4 | 4 | KEEP |
| CampaignSetupTests.cs | Bildiriş / E-poçt / Kampaniya | 3 | 3 | KEEP |
| CheckAssistantCommandTests.cs | Dəstək / Köməkçi / Səs | 7 | 11 | KEEP |
| CheckEmailCommandTests.cs | Bildiriş / E-poçt / Kampaniya | 2 | 2 | KEEP |
| CmsContentEndpointTests.cs | CMS / Məzmun / Media | 8 | 11 | KEEP |
| ConcurrentBookingTests.cs | Booking | 1 | 1 | KEEP |
| ConnectionJourneyTests.cs | Booking | 11 | 12 | KEEP |
| DemoNetworkSeedTests.cs | Uçuş / Axtarış / Cədvəl | 2 | 2 | FIX (zaman qurğusu): sabit 2026-12-14 tarixi; həmin gündən sonra keçmiş gün üçün uçuş yaradılması yoxlanılmalıdır |
| DemoSeedTests.cs | Uçuş / Axtarış / Cədvəl | 2 | 2 | KEEP |
| DestinationCatalogEndpointTests.cs | CMS / Məzmun / Media | 15 | 17 | KEEP |
| DestinationDeleteEndpointTests.cs | CMS / Məzmun / Media | 3 | 3 | KEEP |
| DevelopmentAdminStartupTests.cs | Avtorizasiya / Rollar | 5 | 5 | KEEP |
| DuplicateDocumentNumberTests.cs | Booking | 6 | 10 | KEEP |
| EndToEndTests.cs | Booking | 1 | 1 | KEEP |
| FlightGenerationIntegrationTests.cs | Uçuş / Axtarış / Cədvəl | 4 | 4 | KEEP |
| GlobalExceptionHandlerTests.cs | Infrastruktur / Kontrakt | 3 | 6 | KEEP |
| InfrastructureTests.cs | Infrastruktur / Kontrakt | 5 | 6 | KEEP |
| LandingHeroVideoEndpointTests.cs | CMS / Məzmun / Media | 5 | 5 | KEEP |
| MediaLibraryEndpointTests.cs | CMS / Məzmun / Media | 3 | 3 | KEEP |
| NewsletterEndpointTests.cs | Bildiriş / E-poçt / Kampaniya | 8 | 10 | KEEP |
| PassengerDetailsEndpointTests.cs | Booking | 3 | 3 | KEEP |
| PaymentAndNotificationEndpointTests.cs | Ödəniş / Stripe / Webhook | 4 | 4 | KEEP |
| ProfileAutofillEndpointTests.cs | Booking | 5 | 5 | KEEP |
| RefundEndpointTests.cs | Geri ödəmə / Ləğv | 5 | 5 | KEEP |
| RoleAuthorizationEndpointTests.cs | Autentifikasiya | 14 | 14 | FIX (1/14): CreateAccountAsync(User) təsdiqsiz hesab yaradır, e-poçt qurulu olduqda giriş 401 verir |
| SearchAndOptionsEndpointTests.cs | Uçuş / Axtarış / Cədvəl | 10 | 10 | KEEP |
| SignInToBookEndpointTests.cs | Booking | 2 | 2 | KEEP |
| SiteContentEndpointTests.cs | CMS / Məzmun / Media | 8 | 8 | KEEP |
| StripeCheckoutEndpointTests.cs | Ödəniş / Stripe / Webhook | 12 | 12 | KEEP; 2 ad-təkrarı CheckoutServiceTests ilə qatlararası üst-üstə düşür (DUPLICATE-qismən) |
| SuperAdminProvisioningStartupTests.cs | Avtorizasiya / Rollar | 10 | 10 | KEEP |
| SupportEndpointTests.cs | Dəstək / Köməkçi / Səs | 10 | 13 | KEEP |
| SupportHubTests.cs | Dəstək / Köməkçi / Səs | 6 | 6 | KEEP |
| SupportSetupTests.cs | Dəstək / Köməkçi / Səs | 1 | 1 | KEEP |
| SwaggerContractTests.cs | Infrastruktur / Kontrakt | 2 | 2 | KEEP: 187 endpoint sətri (HTTP metod/yol/avtorizasiya) — sənəd-kontrakt testi, davranış testi deyil |
| VerifiedBookingEndpointTests.cs | Booking | 2 | 2 | FIX (1/2): köməkçi hesabı avtomatik təsdiqləyir, 'təsdiqsiz hesab bloklanır' ssenarisi qurulmur |
| VoiceSearchEndpointTests.cs | Uçuş / Axtarış / Cədvəl | 18 | 21 | KEEP |

Qeyd: cədvəldə 'Sətir' Theory `InlineData` sayının təxmini göstəricisidir; dəqiq say yuxarıdakı işləmə nəticələridir (952 və 562).

## 4. Təkrar və köhnəlmiş testlər

- **Eyni layihədə eyni mətnli təkrar test tapılmadı** (6 tokenli gövdə oxşarlığı ≥ 0.8 və ya ≥ 0.6 + ad oxşarlığı üzrə 0 cüt).
- **Qatlararası (Service ↔ API) ad üst-üstə düşməsi cəmi 8 cütdür**, əsas hissəsi qatları fərqlidir (fake ilə qayda ↔ real DB + HTTP), ona görə DUPLICATE-qismən sayılır, silinməsi tövsiyə olunmur:
  - `APaidBooking_GetsNoSecondPage` (CheckoutServiceTests ↔ StripeCheckoutEndpointTests) — ad 100 % eynidir.
  - `ThePretendPayment_IsRefusedOnceStripeIsConfigured` (eyni iki sinif).
  - `AnUnknownId_IsNotFound_InAzerbaijani` (SiteContentTests ↔ SiteContentEndpointTests).
  - Qalan 5 cüt 'XRules_AreEnforced' ad şablonu oxşarlığıdır, məzmun fərqlidir.
- **Köhnəlmiş (OBSOLETE) fərziyyələr:** BookingConfirmed e-poçtu, Checkout üçün WebhookSecret tələbi, sinxron link e-poçtu, RegisterAsync-dən əvvəlki 'təsdiqsiz hesab' fərziyyəsi.
- **Mövcud olmayan endpoint/service/entity-yə aid test tapılmadı:** 193 controller əməliyyatından yalnız 6-sı üçün funksional test yoxdur (aşağıda), silinmiş sinifləri çağıran test yoxdur (hamısı build olunur).

## 5. Əhatə boşluqları

1. **Repozitoriyalar** (33 sinif) üçün birbaşa test yoxdur; yalnız API testləri vasitəsilə yoxlanılır. Service testlərindəki fake-lər EF sorğularının real davranışını təkrar etmir.
2. **Funksional testi olmayan endpoint-lər** (yalnız `SwaggerContractTests`-də var; statik axtarışla): `GET api/admin/aircraft/overlaps`, `GET api/airports/network`, `POST api/admin/hero-videos/upload/video`, `POST api/admin/hero-videos/upload/poster`, `POST api/auth/resend-verification-email`, `POST api/bookings/cancel-confirm`. Birincisi və ikincisi bu sessiyada əlavə etdiyim endpoint-lərdir və yalnız kontrakt cədvəlindədir.
3. **Yeni repozitoriya məntiqi** (`GetOverlapsAsync`, `GetActiveRoutePairsAsync`, `GetConnectionAirportCodesAsync`) və `route-check`-in `via` sahəsi real uçuşlarla test edilməyib.
4. **Stripe adapterləri:** `StripeCheckoutGateway`, `StripeRefundGateway` testsizdir (SDK sarğıdır). Webhook imza yoxlaması isə güclüdür: real `EventUtility.ConstructEvent` və real HMAC imza ilə.
5. **Arxa plan işçiləri:** `BookingExpiryWorker`, `EmailCampaignWorker`, `NotificationEmailWorker`, `StartupWarmUpWorker` üçün birbaşa test yoxdur (`BookingLifecycleWorker`, `FlightGenerationWorker` üçün var).
6. **Seeder-lər:** `SeedRegionalNetworkAsync` üçün seeding testi yoxdur (yalnız məlumat testi var); `DestinationCatalogSeeder`, `CmsListSeeder`, `LandingHeroVideoSeeder` birbaşa testsizdir.
7. **Paralel bron:** yalnız bir ssenari (`ConcurrentBookingTests`: eyni oturacaq). Webhook-un paralel təkrarı, paralel ödəniş yoxlanmır.
8. **`UserStateCache`-in yeni sessiya metodları** (`TryGetSession/SetSession/Invalidate`) birbaşa testsizdir (API testləri dolayı örtür).
9. **Frontend** üçün heç bir avtomatik test yoxdur (bu auditin əhatəsi xaricindədir).

## 6. Sahə üzrə əhatə (güclü / orta / zəif)

| Sahə | Qiymət | Əsas sübut |
|---|---|---|
| Booking | Güclü | BookingService 19, lifecycle 6+5, passenger types 10, concurrent 1, journeys 11, bron API 8 |
| Oturacaq | Güclü | FlightSearchAndSeat 16, RoundTripSeat 7, SeatPaymentRule 11, AdminSeatMap 3 |
| Ödəniş / Stripe / Webhook | Güclü (adapterlər istisna) | Checkout 17, Webhook 16, RefundWebhook 10, StripeCheckout API 12; real HMAC imza |
| Geri ödəmə | Güclü | RefundService 13, RefundEmail 10, Cancellation* 21, RefundEndpoint 5 |
| Autentifikasiya | Güclü, lakin 8 test sürüşüb | Auth/ResetCode/AccountEmail/AccountSettings/AccountDeletion |
| Avtorizasiya | Orta-güclü | RoleAuthorization 14 + Swagger kontrakt 187 sətir (auth bayrağı) |
| Admin | Güclü | 67 service + ~60 API testi |
| CMS | Orta | SiteContent 27+8, CmsContent 8, media 10; Faq/HomeCard birbaşa az |

## 7. İnteqrasiya/Unit ayrımı

Qatlama düzgündür: Service.Tests-də DB yoxdur, API.Tests-də real DB var. Yeganə nöqsan: Service.Tests-dəki `DemoNetworkDataTests`, `RegionalNetworkDataTests`, `DestinationCatalogDataTests` davranış yox, **sabit məlumat cədvəllərini** yoxlayır (faydalı mühafizədir, amma sabit rəqəmlərə bağlıdır). Test həqiqətən istehsal kodunu çağırır: mock nəticəsini təsdiq edən test (fake-in özünü test edən) aşkar edilmədi.

## 8. Keyfiyyət göstəriciləri

- Skip edilmiş test: **0**. `Thread.Sleep`: 0. `Task.Delay`: 8 yerdə (əksəri istehsal gecikməsini simulyasiya edir). Boş `Assert.True(true)`: 0.
- 'Sıfır assert' siyahısı (16 metod) əsasən yalnız ifadə-gövdəli `=> Assert.…` Theory-lərdir (təhlil metodu onları görmür); əl ilə yoxlanılmalıdır, ancaq real boş test aşkarlanmayıb.
- **Vaxt asılılığı:** Service.Tests sabit saatdan istifadə edir (yaxşı). API.Tests-də 46 yerdə `DateTime.UtcNow`. **Zaman qurğusu:** `DemoNetworkFlightsTests` sabit `2026-12-14` tarixinə uçuş yaradır; həmin tarixdən sonra davranış yoxlanılmalıdır. Demo cədvəllər 2027-01-31-də bitir; `DemoSeedTests` bugündən +3 gün istifadə edir və həmin tarixdən sonra uçuş tapmaya bilər.
- API testləri hər sinif üçün bir DB yaradıb silir; işləmədən sonra serverdə **2 köhnə `AerovaDb_ApiTests_*` bazası** qalıb (əvvəlki yarımçıq işləmədən; bu auditin yaratdığı deyil, təsdiq etmək mümkün deyil).

## 9. CI / GitHub Actions hazırlığı

**Hazır deyil.**

- Depoda **heç bir workflow faylı yoxdur** (`.github`, azure-pipelines, Dockerfile yoxdur).
- API testləri `Server=<AEROVA_TEST_SQLSERVER ya .\SQLEXPRESS>;Trusted_Connection=True` istifadə edir: yalnız **Windows autentifikasiyası**. Server adı mühit dəyişəni ilə dəyişir, amma SQL login (`User Id/Password`) dəstəyi yoxdur. `ubuntu-latest` + SQL Server konteyneri işləməz; `windows-latest` + SQL Express/LocalDB və ya əlaqə sətrində SQL autentifikasiya dəstəyi lazımdır.
- Service.Tests heç bir xarici asılılıq tələb etmir: CI üçün hazırdır (yalnız `dotnet test Service.Tests`). Amma 12 test hazırda qırmızıdır.
- `global.json` yoxdur; hədəf net8.0-dır (CI-də .NET 8 SDK quraşdırılmalıdır). xUnit paralelliyi üçün `xunit.runner.json`/runsettings yoxdur; mən `MaxParallelThreads=2` ilə işlətdim (4 d 40 san).
- `coverlet.collector` mövcuddur, amma əhatə hesabatı konfiqurasiya olunmayıb.

## 10. Silinməsi təklif olunan testlər

**Əsaslandırıla bilən silmə namizədi (REMOVE CANDIDATE) yoxdur.** Bütün uğursuz testlər real iş qaydalarını yoxlayır; onlar silinməməli, düzəldilməlidir. Yeganə seçim: qatlararası 3 təkrar cütdən Service tərəfini (`CheckoutServiceTests.APaidBooking_GetsNoSecondPage`, `ThePretendPayment_IsRefusedOnceStripeIsConfigured`, `SiteContentTests.AnUnknownId_IsNotFound_InAzerbaijani`) saxlamamaq olar, ancaq qazanc kiçikdir və tövsiyə etmirəm.

## 11. Tövsiyələr (prioritet sırası ilə)

1. `RegisterAsync`/`CreateAccountAsync` köməkçilərini aydınlaşdırın: 'təsdiqli hesab' və 'təsdiqsiz hesab' üçün ayrı metodlar. Bu dəyişiklik, ehtimal ki, 6 API testini düzəldir (hər birini yenidən işlətmədən təsdiq etmirəm).
2. Service.Tests-də `BookingService`-ə `IServiceScopeFactory` verin və ya link göndərmə servisini birbaşa çağırın (7 test); `NotificationEmailTests`-də BookingConfirmed əvəzinə göndərilən növ seçin (4 test); Checkout testini yeni qayda ilə yazın (1 test).
3. Yeni endpoint-lər üçün funksional test əlavə edin (`aircraft/overlaps`, `airports/network`, hero-video yükləmə, `resend-verification-email`, `cancel-confirm`).
4. Sabit tarixli testləri (`2026-12-14`) nisbi tarixə çevirin və 2027-01-31 sonrası üçün demo məlumatı yoxlayın.
5. CI: Windows runner + SQL Express (ən sürətli yol) və ya `Trusted_Connection` əvəzinə SQL login dəstəyi; Service.Tests-i ayrıca iş kimi dərhal qoşun.
6. Köhnə `AerovaDb_ApiTests_*` bazalarını təmizləyən addım və `xunit.runner.json` əlavə edin.

Metod: təhlil skriptləri `%TEMP%` və sessiya qovluğundadır; sayımlar `[Fact]/[Theory]` atributlarını oxumaqla, işləmə nəticələri `dotnet test` (trx) ilə alınıb.
