using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using ExcelDataReader;
using Microsoft.Data.SqlClient;
namespace GuvenlikKontrol.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string _connStr;
    private readonly string _dataKlasoru;
    private readonly string _islendiKlasoru;
    private readonly string _raporKlasoru;
    private readonly string _logKlasoru;
    private readonly string _gKontrolKlasoru;

    private readonly Channel<string> _dosyaKanali = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });

    public Worker(ILogger<Worker> logger, IConfiguration config)
    {
        _logger           = logger;
        _connStr          = config.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("DefaultConnection bulunamadı.");
        _dataKlasoru      = config["WorkerAyarlari:DataKlasoru"]     ?? @"C:\Users\User\Desktop\G.Kontrol\Data";
        _islendiKlasoru   = config["WorkerAyarlari:IslendiKlasoru"]  ?? @"C:\Users\User\Desktop\G.Kontrol\Data\Islendi";
        _raporKlasoru     = config["WorkerAyarlari:RaporKlasoru"]    ?? @"C:\Users\User\Desktop\G.Kontrol\Rapor";
        _logKlasoru       = config["WorkerAyarlari:LogKlasoru"]      ?? @"C:\Users\User\Desktop\G.Kontrol\Log";
        _gKontrolKlasoru  = config["WorkerAyarlari:GKontrolKlasoru"] ?? @"C:\Users\User\Desktop\G.Kontrol";
    }

    // ─────────────────────────────────────────────────────────────────
    // ANA DÖNGÜ — FileSystemWatcher + Channel
    // ─────────────────────────────────────────────────────────────────
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_dataKlasoru);
        Directory.CreateDirectory(_islendiKlasoru);
        Directory.CreateDirectory(_raporKlasoru);
        Directory.CreateDirectory(_logKlasoru);

        // Başlangıçta kuyrukta bekleyen dosyaları al
        foreach (var dosya in Directory.GetFiles(_dataKlasoru, "*.xls"))
            _dosyaKanali.Writer.TryWrite(dosya);

        using var watcher = new FileSystemWatcher(_dataKlasoru, "*.xls")
        {
            NotifyFilter        = NotifyFilters.FileName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        watcher.Created += (_, e) =>
        {
            _logger.LogInformation("Yeni dosya algılandı: {Ad}", e.Name);
            _dosyaKanali.Writer.TryWrite(e.FullPath);
        };

        _logger.LogInformation("Worker başlatıldı. İzlenen klasör: {Klasor}", _dataKlasoru);

        await foreach (var dosyaYolu in _dosyaKanali.Reader.ReadAllAsync(ct))
        {
            await Task.Delay(2000, ct); // dosyanın tamamen yazılmasını bekle
            await DosyayiIsleAsync(dosyaYolu, ct);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // İŞLEM AKIŞI
    // ─────────────────────────────────────────────────────────────────
    private async Task DosyayiIsleAsync(string dosyaYolu, CancellationToken ct)
    {
        if (!File.Exists(dosyaYolu))
        {
            _logger.LogWarning("Dosya bulunamadı, atlandı: {Yol}", dosyaYolu);
            return;
        }

        string dosyaAdi   = Path.GetFileName(dosyaYolu);
        var    sw         = Stopwatch.StartNew();
        int    eklenen    = 0, atlanan = 0;
        DateTime? basTarih = null, bitTarih = null;
        string raporAdi   = string.Empty;
        string hataMesaji = string.Empty;
        bool   basarili   = false;

        try
        {
            // ── ADIM 1: XLS OKU + SQL MERGE ─────────────────────────
            _logger.LogInformation("[{Ad}] ADIM 1 başlıyor...", dosyaAdi);
            var satirlar = XlsOku(dosyaYolu);

            if (satirlar.Count == 0)
                throw new InvalidDataException("XLS dosyasında geçerli satır bulunamadı.");

            (eklenen, atlanan) = await KayitlariMergeAsync(satirlar, ct);
            basTarih = satirlar.Min(r => r.DevriyeZamani);
            bitTarih = satirlar.Max(r => r.DevriyeZamani);

            _logger.LogInformation("[{Ad}] ADIM 1 OK → Eklenen: {E}, Atlanan: {A}, " +
                "Aralık: {Bas:dd.MM.yyyy HH:mm} — {Bit:dd.MM.yyyy HH:mm}",
                dosyaAdi, eklenen, atlanan, basTarih, bitTarih);

            // ── ADIM 2: sp_TurAnalizi ÇAĞIR ─────────────────────────
            _logger.LogInformation("[{Ad}] ADIM 2 başlıyor...", dosyaAdi);
            await SpTurAnaliziCagirAsync(basTarih.Value, bitTarih.Value, ct);
            _logger.LogInformation("[{Ad}] ADIM 2 OK → sp_TurAnalizi çağrıldı.", dosyaAdi);

            // ── ADIM 3: EXCEL RAPORU (COM Interop, STA thread) ──────
            _logger.LogInformation("[{Ad}] ADIM 3 başlıyor...", dosyaAdi);
            raporAdi = await Task.Run(
                () => ExcelRaporuOlusturSta(basTarih.Value, bitTarih.Value), ct);
            _logger.LogInformation("[{Ad}] ADIM 3 OK → Rapor: {Rapor}", dosyaAdi, raporAdi);

            basarili = true;
            DosyayiTasi(dosyaYolu); // SADECE başarıda taşı
        }
        catch (Exception ex)
        {
            hataMesaji = ex.Message;
            _logger.LogError(ex, "[{Ad}] HATA — dosya taşınmıyor, yeniden işlenebilir.", dosyaAdi);
            // Dosya TAŞINMIYOR → bir sonraki Worker başlatmada yeniden denenecek
        }
        finally
        {
            sw.Stop();
            // ── ADIM 4: LOG ─────────────────────────────────────────
            await DosyaLogYazAsync(dosyaAdi, eklenen, atlanan, raporAdi,
                sw.ElapsedMilliseconds, basarili, hataMesaji, ct);
            await IslemeLogTablosuYazAsync(dosyaAdi, eklenen, atlanan,
                basTarih, bitTarih, basarili, hataMesaji, ct);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // ADIM 1-A — XLS OKU (B:E kolonları, 2. satırdan itibaren)
    // ─────────────────────────────────────────────────────────────────
    private static List<XlsSatir> XlsOku(string dosyaYolu)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var satirlar = new List<XlsSatir>();

        using var stream = File.Open(dosyaYolu, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var tablo = reader.AsDataSet().Tables[0];

        for (int i = 1; i < tablo.Rows.Count; i++) // i=1 → başlık satırını atla
        {
            var satir = tablo.Rows[i];

            // B → index 1 (BekciAdi)
            string bekci = satir[1]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(bekci)) continue;

            string  nokta   = satir[2]?.ToString()?.Trim() ?? string.Empty; // C
            string? okuyucu = satir[3]?.ToString()?.Trim();                  // D
            if (string.IsNullOrEmpty(okuyucu)) okuyucu = null;

            // E → index 4 (DevriyeZamani) — ExcelDataReader bazen DateTime, bazen string döner
            var zamanRaw = satir[4];
            DateTime zaman;
            if (zamanRaw is DateTime dt)
                zaman = dt;
            else if (!DateTime.TryParse(zamanRaw?.ToString(), out zaman))
                continue;

            satirlar.Add(new XlsSatir(bekci, nokta, okuyucu, zaman));
        }

        return satirlar;
    }

    // ─────────────────────────────────────────────────────────────────
    // ADIM 1-B — SQL MERGE (duplicate-safe: BekciAdi + Nokta + Zaman)
    // ─────────────────────────────────────────────────────────────────
    private async Task<(int eklenen, int atlanan)> KayitlariMergeAsync(
        List<XlsSatir> satirlar, CancellationToken ct)
    {
        const string sql = """
            MERGE BekciKontrolKayitlari AS T
            USING (VALUES (@BekciAdi, @KontrolNoktasiAdi, @OkuyucuKodu, @DevriyeZamani))
                  AS S (BekciAdi, KontrolNoktasiAdi, OkuyucuKodu, DevriyeZamani)
            ON  T.BekciAdi          = S.BekciAdi
            AND T.KontrolNoktasiAdi = S.KontrolNoktasiAdi
            AND T.DevriyeZamani     = S.DevriyeZamani
            WHEN NOT MATCHED THEN
                INSERT (BekciAdi, KontrolNoktasiAdi, OkuyucuKodu, DevriyeZamani)
                VALUES (S.BekciAdi, S.KontrolNoktasiAdi, S.OkuyucuKodu, S.DevriyeZamani);
            """;

        int eklenen = 0, atlanan = 0;

        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);

        foreach (var s in satirlar)
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@BekciAdi",          s.BekciAdi);
            cmd.Parameters.AddWithValue("@KontrolNoktasiAdi", s.KontrolNoktasiAdi);
            cmd.Parameters.AddWithValue("@OkuyucuKodu",       (object?)s.OkuyucuKodu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DevriyeZamani",     s.DevriyeZamani);

            int etkilenen = await cmd.ExecuteNonQueryAsync(ct);
            if (etkilenen > 0) eklenen++; else atlanan++;
        }

        return (eklenen, atlanan);
    }

    // ─────────────────────────────────────────────────────────────────
    // ADIM 2 — sp_TurAnalizi çağır
    // ─────────────────────────────────────────────────────────────────
    private async Task SpTurAnaliziCagirAsync(DateTime basTarih, DateTime bitTarih, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connStr);
        await using var cmd  = new SqlCommand("sp_TurAnalizi", conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = 120
        };
        cmd.Parameters.Add("@BasTarih", SqlDbType.DateTime).Value = basTarih;
        cmd.Parameters.Add("@BitTarih", SqlDbType.DateTime).Value = bitTarih;

        await conn.OpenAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ─────────────────────────────────────────────────────────────────
    // ADIM 3 — Excel COM Interop (STA thread zorunlu)
    // ─────────────────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private string ExcelRaporuOlusturSta(DateTime basTarih, DateTime bitTarih)
    {
        string?    raporAdi = null;
        Exception? hata     = null;
        int        excelPid = -1;

        var thread = new Thread(() =>
        {
            // dynamic (late binding) — office.dll / interop DLL'e gerek yok
            dynamic? excelApp = null;
            dynamic? xlsb     = null;
            dynamic? raporWb  = null;

            try
            {
                // Excel COM nesnesini geç bağlama ile oluştur
                var excelType = Type.GetTypeFromProgID("Excel.Application")
                    ?? throw new InvalidOperationException(
                        "Excel kurulu değil veya COM kaydı bulunamadı.");

                excelApp = Activator.CreateInstance(excelType)!;
                excelApp.Visible          = false;
                excelApp.DisplayAlerts    = false;
                excelApp.AskToUpdateLinks = false;
                excelApp.Interactive      = false; // VBA MsgBox'ları otomatik OK ile geç

                // Excel process PID'ini yakala
                GetWindowThreadProcessId(new IntPtr((int)excelApp.Hwnd), out uint pid);
                excelPid = (int)pid;

                // G.Kontrol klasöründeki *.xlsb dosyasını bul
                var xlsbDosyalari = Directory.GetFiles(_gKontrolKlasoru, "*.xlsb");
                if (xlsbDosyalari.Length == 0)
                    throw new FileNotFoundException(
                        $"*.xlsb dosyası bulunamadı: {_gKontrolKlasoru}");

                string xlsbYolu = xlsbDosyalari[0];
                _logger.LogInformation("xlsb açılıyor: {Yol}", xlsbYolu);
                xlsb = excelApp.Workbooks.Open(xlsbYolu, 0, false); // UpdateLinks=0, ReadOnly=false
                excelApp.DisplayAlerts = false; // Workbooks.Open sonrası sıfırlanmış olabilir

                // Makro 1: TurAnaliziGuncel_t(basTarih, bitTarih)
                excelApp.Run("Module1.TurAnaliziGuncel_t", basTarih, bitTarih);
                excelApp.DisplayAlerts = false; // Makro sonrası sıfırlanmış olabilir
                excelApp.Interactive   = false;

                // Makro 2: Rapor
                excelApp.Run("mdlRapor.Rapor");
                excelApp.DisplayAlerts = false; // Makro sonrası sıfırlanmış olabilir
                excelApp.Interactive   = false;

                // İlk sayfayı argümansız Copy() ile yeni workbook'a taşı
                // → Workbooks.Add() kullanmıyoruz, Sheet1 hiç oluşmuyor, silme diyalogu çıkmıyor
                var sayfaAdlari = new[]
                    { "Analiz", "Eksik_Noktalar", "Gunluk_Tur_Raporu", "Bekci_Performansı" };

                ((dynamic)xlsb.Worksheets[sayfaAdlari[0]]).Copy(); // yeni wb = sadece bu sayfa
                raporWb = excelApp.ActiveWorkbook;

                // Geri kalan 3 sayfayı kopyala
                for (int i = 1; i < sayfaAdlari.Length; i++)
                {
                    dynamic ws = xlsb.Worksheets[sayfaAdlari[i]];
                    ws.Copy(After: raporWb.Worksheets[(int)raporWb.Worksheets.Count]);
                }

                // Raporu kaydet (51 = xlOpenXMLWorkbook / .xlsx)
                Directory.CreateDirectory(_raporKlasoru);
                raporAdi = $"{basTarih:dd.MM.yyyy}-{bitTarih:dd.MM.yyyy}.xlsx";
                string raporTamYol = Path.Combine(_raporKlasoru, raporAdi);
                raporWb.SaveAs(raporTamYol, 51);

                raporWb.Close(false);
                raporWb = null;

                xlsb.Close(false);
                xlsb = null;

                excelApp.Quit();
                excelApp = null;
            }
            catch (Exception ex)
            {
                hata = ex;
                _logger.LogError(ex, "Excel COM hatası");
            }
            finally
            {
                try { raporWb?.Close(false); }  catch { }
                try { xlsb?.Close(false); }     catch { }
                try { excelApp?.Quit(); }        catch { }

                if (raporWb  != null) Marshal.ReleaseComObject(raporWb);
                if (xlsb     != null) Marshal.ReleaseComObject(xlsb);
                if (excelApp != null) Marshal.ReleaseComObject(excelApp);

                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (hata != null && excelPid > 0)
                {
                    try { Process.GetProcessById(excelPid).Kill(); } catch { }
                    _logger.LogWarning("Orphan Excel process öldürüldü. PID: {Pid}", excelPid);
                }
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (hata != null)
            throw new Exception($"Excel raporu oluşturulamadı: {hata.Message}", hata);

        return raporAdi ?? string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────
    // ADIM 4-A — Günlük dosya logu yaz
    // ─────────────────────────────────────────────────────────────────
    private async Task DosyaLogYazAsync(
        string dosyaAdi, int eklenen, int atlanan,
        string raporAdi, long sureMilis, bool basarili,
        string hataMesaji, CancellationToken ct)
    {
        try
        {
            string logDosyasi = Path.Combine(_logKlasoru, $"{DateTime.Now:yyyy-MM-dd}.log");
            string durum      = basarili ? "BAŞARILI" : "HATA";
            string raporBilgi = string.IsNullOrEmpty(raporAdi) ? "(rapor yok)" : raporAdi;

            string satir = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " +
                           $"Dosya: {dosyaAdi} | " +
                           $"Eklenen: {eklenen} | Atlanan: {atlanan} | " +
                           $"Rapor: {raporBilgi} | Süre: {sureMilis}ms | " +
                           $"Durum: {durum}";

            if (!basarili && !string.IsNullOrEmpty(hataMesaji))
                satir += $" | Hata: {hataMesaji}";

            await File.AppendAllTextAsync(logDosyasi,
                satir + Environment.NewLine, Encoding.UTF8, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Log dosyası yazılamadı");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // ADIM 4-B — DosyaIslemeLog tablosuna yaz (web'in Son Güncelleme için)
    // ─────────────────────────────────────────────────────────────────
    private async Task IslemeLogTablosuYazAsync(
        string dosyaAdi, int eklenen, int atlanan,
        DateTime? basTarih, DateTime? bitTarih,
        bool basarili, string hataMesaji, CancellationToken ct)
    {
        try
        {
            const string sql = """
                INSERT INTO DosyaIslemeLog
                    (DosyaAdi, IslenmeTarihi, EklenenKayit, AtlananKayit,
                     BasTarih, BitTarih, Durum, HataMesaji)
                VALUES
                    (@DosyaAdi, @IslenmeTarihi, @EklenenKayit, @AtlananKayit,
                     @BasTarih, @BitTarih, @Durum, @HataMesaji)
                """;

            await using var conn = new SqlConnection(_connStr);
            await using var cmd  = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@DosyaAdi",      dosyaAdi);
            cmd.Parameters.AddWithValue("@IslenmeTarihi", DateTime.Now);
            cmd.Parameters.AddWithValue("@EklenenKayit",  eklenen);
            cmd.Parameters.AddWithValue("@AtlananKayit",  atlanan);
            cmd.Parameters.AddWithValue("@BasTarih",      (object?)basTarih  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BitTarih",      (object?)bitTarih  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Durum",         basarili ? "Basarili" : "Hata");
            cmd.Parameters.AddWithValue("@HataMesaji",
                string.IsNullOrEmpty(hataMesaji) ? DBNull.Value : (object)hataMesaji);

            await conn.OpenAsync(ct);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DosyaIslemeLog kaydı yazılamadı: {Ad}", dosyaAdi);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // YARDIMCI — Başarılı dosyayı Islendi klasörüne taşı
    // ─────────────────────────────────────────────────────────────────
    private void DosyayiTasi(string dosyaYolu)
    {
        try
        {
            string ad    = Path.GetFileNameWithoutExtension(dosyaYolu);
            string ext   = Path.GetExtension(dosyaYolu);
            string hedef = Path.Combine(_islendiKlasoru,
                $"{ad}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");

            Directory.CreateDirectory(_islendiKlasoru);
            File.Move(dosyaYolu, hedef, overwrite: true);
            _logger.LogInformation("Dosya taşındı → {Hedef}", hedef);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dosya taşınamadı: {Yol}", dosyaYolu);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // İÇ TİP
    // ─────────────────────────────────────────────────────────────────
    private record XlsSatir(
        string   BekciAdi,
        string   KontrolNoktasiAdi,
        string?  OkuyucuKodu,
        DateTime DevriyeZamani);
}
