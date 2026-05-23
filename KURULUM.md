# Güvenlik Kontrol Sistemi — Kurulum Rehberi

## Ön Koşullar

| Gereksinim | Notlar |
|-----------|--------|
| Windows 10/11 veya Windows Server 2019+ | |
| .NET 8 Runtime | https://dotnet.microsoft.com/download/dotnet/8.0 → "Run server apps" |
| SQL Server 2019+ (Express yeterli) | |
| Microsoft Excel | xlsb dosyasındaki makrolar için zorunlu |
| SSMS (SQL Server Management Studio) | Veritabanı kurulumu için |

---

## ADIM 1 — Klasör Yapısını Oluşturun

İşyerindeki bilgisayarda aşağıdaki klasörleri oluşturun. Yollar tamamen serbesttir — sonraki adımda `appsettings.json`'a yazacaksınız.

**Önerilen yapı:**

```
D:\GKontrol\              ← xlsb dosyası buraya
D:\GKontrol\Data\         ← İşlenecek XLS dosyaları buraya atılır
D:\GKontrol\Data\Islendi\ ← İşlenen XLS'ler otomatik buraya taşınır
D:\GKontrol\Rapor\        ← Oluşturulan Excel raporları buraya kaydedilir
D:\GKontrol\Log\          ← Günlük log dosyaları
```

> **Not:** Klasör yolları ne olursa olsun, hepsini `appsettings.json`'a yazmanız yeterli. Kod otomatik oluşturur.

---

## ADIM 2 — Veritabanını Kurun

### 2a. Tabloları oluşturun
SSMS'i **sa** veya sysadmin yetkili kullanıcıyla açın, `SQL/01_veritabani_kur.sql` dosyasını çalıştırın.

### 2b. Stored Procedure'leri aktarın
`sp_TurAnalizi` ve `sp_BekciPerformansi` kaynak sunucudan şu şekilde alınır:

1. Kaynak bilgisayarda SSMS açın
2. `G.Kontrol_Automation` → **Programmability** → **Stored Procedures**
3. `sp_TurAnalizi` üzerine sağ tık → **Script Stored Procedure As** → **CREATE To** → **New Query Window**
4. Açılan scripti kopyalayıp işyeri SQL Server'ında çalıştırın
5. `sp_BekciPerformansi` için aynı işlemi tekrarlayın

---

## ADIM 3 — xlsb Dosyasını Kopyalayın

Mevcut bilgisayardan `*.xlsb` dosyasını alıp işyeri bilgisayarında **GKontrolKlasoru** olarak belirleyeceğiniz klasöre kopyalayın (örn. `D:\GKontrol\`).

---

## ADIM 4 — Worker appsettings.json Düzenleyin

`GuvenlikKontrol.Worker\appsettings.json` dosyasını işyerindeki gerçek değerlere göre güncelleyin:

```json
{
  "WorkerAyarlari": {
    "DataKlasoru":     "D:\\GKontrol\\Data",
    "IslendiKlasoru":  "D:\\GKontrol\\Data\\Islendi",
    "RaporKlasoru":    "D:\\GKontrol\\Rapor",
    "LogKlasoru":      "D:\\GKontrol\\Log",
    "GKontrolKlasoru": "D:\\GKontrol"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=ISYERI-PC\\SQLEXPRESS;Database=G.Kontrol_Automation;User Id=sa;Password=SIFRENIZ;TrustServerCertificate=True;"
  }
}
```

> **Connection String parametreleri:**
> - `Server=` → SQL Server adı (örn. `.\SQLEXPRESS`, `ISYERI-PC\SQLEXPRESS`, `localhost`)
> - `User Id=` + `Password=` → SA veya yetkili SQL kullanıcısı

---

## ADIM 5 — Web appsettings.json Düzenleyin

`GuvenlikKontrolWeb\appsettings.json` dosyasındaki connection string'i aynı şekilde işyeri sunucusuna göre güncelleyin.

---

## ADIM 6 — Worker Servisini Kurun

**Yönetici (Admin) PowerShell** açın ve çalıştırın:

```powershell
# Kaynak klasörden çalıştırın
.\deploy_worker.ps1 -ServisKlasoru "C:\Services\GuvenlikWorker"
```

Veya manuel olarak:

```powershell
# Publish
dotnet publish ".\GuvenlikKontrol.Worker\GuvenlikKontrol.Worker.csproj" -c Release -o "C:\Services\GuvenlikWorker"

# Servis olarak kaydet
sc.exe create "GuvenlikKontrolWorker" binPath= "C:\Services\GuvenlikWorker\GuvenlikKontrol.Worker.exe" start= auto

# Başlat
sc.exe start "GuvenlikKontrolWorker"
```

---

## ADIM 7 — Web Uygulamasını Yayınlayın

```powershell
dotnet publish ".\GuvenlikKontrolWeb\GuvenlikKontrolWeb.csproj" -c Release -o "C:\inetpub\GuvenlikWeb"
```

IIS'e ekleyin veya doğrudan çalıştırın:
```powershell
cd "C:\inetpub\GuvenlikWeb"
.\GuvenlikKontrolWeb.exe
```

---

## ADIM 8 — Test

1. `Data\` klasörüne bir `.xls` dosyası atın
2. ~5 saniye bekleyin
3. Kontrol edin:

| Kontrol | Beklenen |
|---------|----------|
| `Log\yyyy-MM-dd.log` | `Durum: BAŞARILI` satırı |
| `Rapor\` klasörü | `dd.MM.yyyy-dd.MM.yyyy.xlsx` oluştu |
| `Data\Islendi\` | XLS dosyası taşındı |
| SSMS → `BekciKontrolKayitlari` | Kayıtlar eklendi |
| Web uygulaması | Dashboard'da veriler görünüyor |

---

## Sorun Giderme

| Hata | Çözüm |
|------|-------|
| `SqlClient is not supported` | Publish'i `-r win-x64` **olmadan** yapın |
| `server not found` | Connection string'deki sunucu adını kontrol edin |
| `xlsb bulunamadı` | `GKontrolKlasoru` yolunun doğru olduğunu kontrol edin |
| `Makro çalışmıyor` | Excel'in makrolara izin verdiğinden emin olun (Trust Center) |
| Servis başlamıyor | `Get-EventLog -LogName Application -Newest 5` ile hata görün |

---

## Klasör Yapısı — Özet

```
[Repo]
├── GuvenlikKontrol.Worker/
│   ├── appsettings.json   ← BURASI DEĞİŞTİRİLECEK (klasörler + connection string)
│   └── Worker.cs
├── GuvenlikKontrolWeb/
│   ├── appsettings.json   ← BURASI DEĞİŞTİRİLECEK (connection string)
│   └── Program.cs
├── SQL/
│   └── 01_veritabani_kur.sql   ← İlk kurulumda SSMS'de çalıştırın
├── deploy_worker.ps1            ← Worker servis kurulum betiği
└── KURULUM.md                   ← Bu dosya
```
