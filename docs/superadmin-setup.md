# İlkin SuperAdmin hesabı

SuperAdmin-i heç bir API sorğusu yarada bilməz: nə qeydiyyat, nə Admin, nə də başqa SuperAdmin. İki server tərəfli yol var və ikisində də
əvvəlcə **verilənlər bazası miqrasiyaları tətbiq olunmalıdır** (`dotnet ef database update`, yedək aldıqdan sonra).

| Vəziyyət | Yol |
| --- | --- |
| SuperAdmin hələ yoxdur, yeni hesab istəyirsiniz | **A. Bootstrap** (başlanğıcda bir dəfə) |
| Artıq işlək bir Admin hesabınız var (məs. `admin@aerova.com`) | **B. promote-superadmin əmri** |

Sistem heç vaxt mövcud hesabı özü yüksəltmir və yalnız e-poçt ünvanına baxıb səssizcə SuperAdmin etmir.

## A. Bootstrap: yeni SuperAdmin

Üç açar lazımdır. Hamısı **User Secrets** və ya **mühit dəyişəni** olmalıdır, `appsettings.json`-a yazmayın.

| Açar | Mühit dəyişəni | Dəyər |
| --- | --- | --- |
| `Bootstrap:Enabled` | `Bootstrap__Enabled` | `true` |
| `Bootstrap:SuperAdminEmail` | `Bootstrap__SuperAdminEmail` | yeni hesabın e-poçtu |
| `Bootstrap:SuperAdminPassword` | `Bootstrap__SuperAdminPassword` | 12-100 simvol, hərf və rəqəm |

Lokal (Development) üçün `AirlineReservationSystem/AirlineReservationSystem` qovluğunda:

```powershell
dotnet user-secrets set "Bootstrap:Enabled" "true"
dotnet user-secrets set "Bootstrap:SuperAdminEmail" "sizin.adresiniz@example.com"
dotnet user-secrets set "Bootstrap:SuperAdminPassword" "12-simvoldan-uzun-parol-1"
```

API-ni başladın. Konsolda bu sətir çıxmalıdır:

```
SuperAdmin account created from the Bootstrap settings. Remove Bootstrap:SuperAdminPassword from the configuration now.
```

Sonra **parolu konfiqurasiyadan silin**, bayrağı da söndürün:

```powershell
dotnet user-secrets remove "Bootstrap:SuperAdminPassword"
dotnet user-secrets set "Bootstrap:Enabled" "false"
```

Qaydalar:
- Aktiv SuperAdmin varsa, bu açarlar **heç nə etmir** ("already exists" sətri çıxır). Unudulmuş açar yeni hesab əlavə edə bilməz.
- E-poçt ünvanını artıq başqa hesab işlədirsə, o hesaba toxunulmur və yüksəldilmir (xəbərdarlıq sətri çıxır). Başqa ünvan seçin və ya B yolunu işlədin.
- Hesab e-poçtu təsdiqlənmiş sayılır, ona görə təsdiq məktubu lazım deyil.
- E-poçt və parol heç vaxt loga yazılmır.

## B. `promote-superadmin` əmri: mövcud Admin-i yüksəltmək

Əmr veb-saytı başlatmır, əməliyyatı bitirib çıxır. Yalnız serverə çıxışı olan operator işlədə bilər, API vasitəsilə çatmaq mümkün deyil.

`AirlineReservationSystem` (həll) qovluğunda:

```powershell
dotnet run --project AirlineReservationSystem --no-launch-profile -- promote-superadmin --email admin@aerova.com
```

Əmr nə edəcəyini yazır və ünvanı **yenidən yazmağınızı** istəyir. Düz yazıb Enter basanda hesab SuperAdmin olur. Skriptlər üçün sual `--yes` ilə
keçilir (yoxlamalar yenə işləyir).

Yalnız bu şərtlərlə yüksəldilir, qalan hallarda heç nə dəyişmir:
- hesab mövcuddur və **Admin**-dir (User əvvəlcə Admin edilməlidir),
- hesab aktivdir,
- e-poçt ünvanı təsdiqlənib.

Çıxış kodları: `0` yüksəldildi, `1` rədd edildi (səbəb yazılır), `2` ünvan verilməyib, `3` miqrasiyalar tətbiq olunmayıb.

Yüksəldilən hesab **yenidən daxil olmalıdır**: köhnə tokeni Admin rolunu daşıyır və növbəti sorğuda (ən gec 30 saniyə sonra) qəbul edilmir.

## Production

- Mühit dəyişənləri ilə (`Bootstrap__...`) verin, bir dəfə işə salın, sonra silin.
- Mühit konfiqurasiya sistemində parolu loglamayan yer seçin.
- `Seed:CreateAdmin` (Development Admin) production-da hələ də işləmir; SuperAdmin bootstrap isə bütün mühitlərdə işləyir.

## Hər iki yolda audit qeydi

- A yolu: `SuperAdminBootstrapped` (kim: server).
- B yolu: `UserRoleChanged`, təfsilat `Admin -> SuperAdmin (server command)` (kim: server).

SuperAdmin panelindəki audit loqunda görünür (`GET /api/admin/audit-logs`).
