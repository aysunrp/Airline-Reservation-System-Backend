# E-poçt (Gmail SMTP) quraşdırması

API e-poçt göndərməsə də işləyir: hesab yaranır, amma e-poçt təsdiqlənmir və "Şifrəni unutdum" `503` qaytarır.
Aşağıdakı açarlar doldurulanda API real məktub göndərir.

## Gmail hesabında
1. Google Hesabı → **Təhlükəsizlik** → **2 mərhələli doğrulama**-nı aktiv edin.
2. https://myaccount.google.com/apppasswords ünvanında ad yazın (məs. `AEROVA`) və **Yarat** basın.
3. Çıxan 16 simvolu (boşluqsuz) kopyalayın. Bu **App password**-dur, adi Gmail parolu deyil.

## User Secrets (parol Git-ə düşməməlidir)
Layihə qovluğunda (`AirlineReservationSystem/AirlineReservationSystem`) PowerShell-də:

```powershell
dotnet user-secrets set "Smtp:UserName"    "sizin.adresiniz@gmail.com"
dotnet user-secrets set "Smtp:SenderEmail" "sizin.adresiniz@gmail.com"
dotnet user-secrets set "Smtp:Password"    "16-simvollu-app-password"
```

Başqa mühitdə eyni açarlar mühit dəyişəni kimi: `Smtp__UserName`, `Smtp__SenderEmail`, `Smtp__Password`.

| Açar | Dəyər | Harada |
| --- | --- | --- |
| `Smtp:Host` | `smtp.gmail.com` | `appsettings.json` (gizli deyil) |
| `Smtp:Port` | `587` | `appsettings.json` |
| `Smtp:Security` | `StartTls` | `appsettings.json` |
| `Smtp:SenderName` | `AEROVA` | `appsettings.json` |
| `Smtp:UserName` | Gmail ünvanı | **User Secrets** |
| `Smtp:SenderEmail` | eyni Gmail ünvanı | **User Secrets** |
| `Smtp:Password` | 16 simvollu App password | **User Secrets** |
| `Auth:FrontendBaseUrl` | saytın ünvanı, məs. `http://localhost:5500` | `appsettings.json` |

Məktubdakı keçidlər `Auth:FrontendBaseUrl` ünvanındakı `verify-email.html` və `reset-password.html` səhifələrinə aparır.

## Digər ayarlar (`Auth` bölməsi)
- `VerifyEmailHours` (24): təsdiq keçidinin müddəti.
- `ResetPasswordMinutes` (60): şifrə sıfırlama keçidinin müddəti.
- `EmailCooldownSeconds` (60): eyni hesaba iki məktub arasındakı minimum fasilə.
- `RequireVerifiedEmailForBooking` (true): daxil olmuş müştəri e-poçtunu təsdiqləmədən bron edə bilmir. Yalnız e-poçt göndərmə quraşdırılıbsa tətbiq olunur.

## Yoxlama
1. API-ni `http` profili ilə işə salın, `login.html`-də **Qeydiyyat** edin (öz real e-poçtunuzla).
2. Gələnlər qutusunda (və Spam-da) "AEROVA" məktubunu tapın və keçidi açın.
3. Məktub gəlmirsə, API konsolunda `E-mail delivery failed (AuthenticationException ...)` kimi sətrə baxın: `AuthenticationException` App password və ya ünvanın səhv olduğunu göstərir.

## Bildirişlər e-poçtla
Rezervasiya təsdiqi, ödəniş, ləğv, geri ödəniş, check-in xatırlatması və uçuş dəyişikliyi bildirişləri (Notifications səhifəsində görünənlər) **hesabın qeydiyyat e-poçtuna** da göndərilir. Qaydalar:
- Yalnız **təsdiqlənmiş** e-poçt ünvanına gedir. Rezervasiyada yazılan əlaqə e-poçtuna heç nə göndərilmir.
- Yuxarıdakı SMTP ayarları işləməlidir. İşləmirsə, bildiriş yalnız proqramda qalır.
- Arxa plan işi hər 20 saniyədən bir yeni bildirişləri göndərir (`BookingLifecycle:WorkerEnabled=false` onu da söndürür). Göndərilməyən məktub 3 dəfə təkrar cəhd olunur, sonra buraxılır.
- 1 gündən köhnə bildirişlər e-poçtla göndərilmir (e-poçtu sonradan işə salanda köhnə xəbərlər yağmasın deyə). Miqrasiya `AddNotificationEmail` mövcud bildirişləri "göndərilib" sayır.
- Məktubun mətni bildirişin özüdür (ingiliscə). Hər bildiriş bir məktubdur.

## E-poçtun işlədiyini yoxlamaq (`check-email`)
Məktub gəlmirsə, əvvəl bu əmri işlədin. O, Gmail serverinə qoşulub daxil olur, **heç bir məktub göndərmir** və nəticəni sadə sözlərlə yazır (login və şifrəni göstərmir):

```powershell
cd AirlineReservationSystem\AirlineReservationSystem
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --no-launch-profile -- check-email
```

Nəticələr: "The mail server accepted the login" (hər şey qaydasındadır, təsdiq linkini profildən yenidən istəyin), "refused the login" (App password və ya giriş ünvanı səhv, ya da hesab App password-a icazə vermir), "could not be reached" (internet, firewall və ya antivirus 587 portunu bağlayır).

Təsdiq linki 24 saat etibarlıdır. Vaxtı keçibsə, profildən yenisini istəyin: köhnə link işləmir.
