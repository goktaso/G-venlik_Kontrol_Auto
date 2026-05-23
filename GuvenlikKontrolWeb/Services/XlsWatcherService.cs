using System.Collections.Concurrent;
using System.Data;
using System.Text;
using ExcelDataReader;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace GuvenlikKontrolWeb.Services
{
    public class XlsWatcherService : BackgroundService
    {
        private readonly ILogger<XlsWatcherService> _logger;
        private readonly XlsWatcherSettings _settings;
        private readonly string _connStr;
        private readonly ConcurrentQueue<string> _fileQueue = new();

        public XlsWatcherService(
            ILogger<XlsWatcherService> logger,
            IOptions<XlsWatcherSettings> settings,
            IConfiguration config)
        {
            _logger = logger;
            _settings = settings.Value;
            _connStr = config.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("DefaultConnection bulunamadı.");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Directory.CreateDirectory(_settings.IzlenenKlasor);
            Directory.CreateDirectory(_settings.IslenmisMiKlasor);

            // Uygulama başlarken bekleyen dosyaları kuyruğa ekle
            foreach (var file in Directory.GetFiles(_settings.IzlenenKlasor, "*.xls"))
                _fileQueue.Enqueue(file);

            using var watcher = new FileSystemWatcher(_settings.IzlenenKlasor, "*.xls")
            {
                NotifyFilter = NotifyFilters.FileName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            watcher.Created += (_, e) =>
            {
                _logger.LogInformation("Yeni XLS dosyası algılandı: {Path}", e.FullPath);
                _fileQueue.Enqueue(e.FullPath);
            };

            _logger.LogInformation("XLS izleyici başlatıldı. Klasör: {Path}", _settings.IzlenenKlasor);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_fileQueue.TryDequeue(out var filePath))
                {
                    // Dosyanın tamamen yazılmasını bekle
                    await Task.Delay(_settings.DosyaBeklemeSuresiMs, stoppingToken);
                    await ProcessFileAsync(filePath, stoppingToken);
                }
                else
                {
                    await Task.Delay(500, stoppingToken);
                }
            }
        }

        private async Task ProcessFileAsync(string filePath, CancellationToken ct)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Dosya bulunamadı, atlanıyor: {Path}", filePath);
                return;
            }

            _logger.LogInformation("İşleniyor: {Path}", filePath);

            try
            {
                var (eklenen, atlanan, minTarih, maxTarih) = await InsertFromXlsAsync(filePath, ct);

                _logger.LogInformation(
                    "{File} → {Eklenen} yeni kayıt eklendi, {Atlanan} atlandı. Tarih aralığı: {Min} - {Max}",
                    Path.GetFileName(filePath), eklenen, atlanan, minTarih, maxTarih);

                if (eklenen > 0 && minTarih.HasValue && maxTarih.HasValue)
                    await CallTurAnaliziAsync(minTarih.Value, maxTarih.Value, ct);

                MoveToProcessed(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya işlenirken hata: {Path}", filePath);
            }
        }

        private async Task<(int eklenen, int atlanan, DateTime? minTarih, DateTime? maxTarih)>
            InsertFromXlsAsync(string filePath, CancellationToken ct)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var rows = new List<(string bekci, string nokta, string? okuyucu, DateTime zaman)>();

            await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataset = reader.AsDataSet();
            var table = dataset.Tables[0];

            for (int i = 1; i < table.Rows.Count; i++)
            {
                var row = table.Rows[i];
                string bekci = row[1]?.ToString()?.Trim() ?? string.Empty;
                string nokta = row[2]?.ToString()?.Trim() ?? string.Empty;
                string? okuyucu = row[3]?.ToString();

                if (!DateTime.TryParse(row[4]?.ToString(), out DateTime zaman))
                    continue;

                if (string.IsNullOrEmpty(bekci))
                    continue;

                rows.Add((bekci, nokta, okuyucu, zaman));
            }

            if (rows.Count == 0)
                return (0, 0, null, null);

            int eklenen = 0, atlanan = 0;

            const string insertSql = """
                INSERT INTO BekciKontrolKayitlari (BekciAdi, KontrolNoktasiAdi, OkuyucuKodu, DevriyeZamani)
                SELECT @BekciAdi, @KontrolNoktasiAdi, @OkuyucuKodu, @DevriyeZamani
                WHERE NOT EXISTS (
                    SELECT 1 FROM BekciKontrolKayitlari
                    WHERE BekciAdi = @BekciAdi AND DevriyeZamani = @DevriyeZamani
                )
                """;

            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync(ct);

            foreach (var (bekci, nokta, okuyucu, zaman) in rows)
            {
                await using var cmd = new SqlCommand(insertSql, conn);
                cmd.Parameters.AddWithValue("@BekciAdi", bekci);
                cmd.Parameters.AddWithValue("@KontrolNoktasiAdi", nokta);
                cmd.Parameters.AddWithValue("@OkuyucuKodu", (object?)okuyucu ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DevriyeZamani", zaman);

                int affected = await cmd.ExecuteNonQueryAsync(ct);
                if (affected > 0) eklenen++;
                else atlanan++;
            }

            DateTime minTarih = rows.Min(r => r.zaman);
            DateTime maxTarih = rows.Max(r => r.zaman);

            return (eklenen, atlanan, minTarih, maxTarih);
        }

        private async Task CallTurAnaliziAsync(DateTime basTarih, DateTime bitTarih, CancellationToken ct)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await using var cmd = new SqlCommand("sp_TurAnalizi", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 120
                };
                cmd.Parameters.Add("@BasTarih", SqlDbType.DateTime).Value = basTarih.Date;
                cmd.Parameters.Add("@BitTarih", SqlDbType.DateTime).Value = bitTarih.Date.AddDays(1).AddSeconds(-1);

                await conn.OpenAsync(ct);
                await cmd.ExecuteNonQueryAsync(ct);

                _logger.LogInformation("sp_TurAnalizi çalıştırıldı. {Bas} - {Bit}", basTarih.Date, bitTarih.Date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "sp_TurAnalizi çağrısında hata");
            }
        }

        private void MoveToProcessed(string filePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath);
                string hedef = Path.Combine(
                    _settings.IslenmisMiKlasor,
                    $"{fileName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");

                File.Move(filePath, hedef, overwrite: true);
                _logger.LogInformation("Dosya taşındı: {Hedef}", hedef);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dosya taşınamadı: {Path}", filePath);
            }
        }
    }
}
