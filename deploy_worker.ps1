# ============================================================
# Güvenlik Kontrol Worker — Kurulum / Güncelleme Betiği
# YÖNETİCİ (Admin) PowerShell ile çalıştırın
# ============================================================

param(
    [string]$ProjeKlasoru  = ".\GuvenlikKontrol.Worker",
    [string]$ServisKlasoru = "C:\Services\GuvenlikWorker",
    [string]$ServisAdi     = "GuvenlikKontrolWorker",
    [string]$ServisAciklama = "Guvenlik Kontrol - XLS Dosya İşleme Servisi"
)

$ErrorActionPreference = "Stop"
Write-Host "`n=== Güvenlik Kontrol Worker Kurulum Betiği ===" -ForegroundColor Cyan

# 1. Servisi durdur (varsa)
$servis = Get-Service -Name $ServisAdi -ErrorAction SilentlyContinue
if ($servis) {
    if ($servis.Status -eq "Running") {
        Write-Host "Servis durduruluyor..." -ForegroundColor Yellow
        sc.exe stop $ServisAdi | Out-Null
        Start-Sleep -Seconds 3
    }
    Write-Host "Servis mevcut: $ServisAdi" -ForegroundColor Gray
} else {
    Write-Host "Servis henüz kurulu değil, ilk kurulum yapılacak." -ForegroundColor Gray
}

# 2. Publish
Write-Host "`nPublish yapılıyor..." -ForegroundColor Cyan
$csproj = Join-Path $ProjeKlasoru "GuvenlikKontrol.Worker.csproj"
dotnet publish $csproj -c Release -o $ServisKlasoru
if ($LASTEXITCODE -ne 0) { throw "Publish başarısız oldu!" }
Write-Host "Publish tamamlandı: $ServisKlasoru" -ForegroundColor Green

# 3. Servisi kaydet (ilk kez)
if (-not $servis) {
    Write-Host "`nServis kaydediliyor..." -ForegroundColor Cyan
    $exePath = Join-Path $ServisKlasoru "GuvenlikKontrol.Worker.exe"
    sc.exe create $ServisAdi binPath= $exePath start= auto DisplayName= $ServisAciklama
    sc.exe description $ServisAdi $ServisAciklama
    Write-Host "Servis kaydedildi: $ServisAdi" -ForegroundColor Green
}

# 4. Servisi başlat
Write-Host "`nServis başlatılıyor..." -ForegroundColor Cyan
sc.exe start $ServisAdi | Out-Null
Start-Sleep -Seconds 2
sc.exe query $ServisAdi

Write-Host "`n=== TAMAMLANDI ===" -ForegroundColor Green
Write-Host "Log klasörü: Bkz. appsettings.json > WorkerAyarlari:LogKlasoru" -ForegroundColor Gray
