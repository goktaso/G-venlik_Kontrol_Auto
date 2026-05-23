using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

public class IndexModel : PageModel
{
    private readonly IConfiguration _config;

    public DateTime? SonGuncelleme { get; private set; }
    public string SonBasTarih { get; private set; } = string.Empty;
    public string SonBitTarih { get; private set; } = string.Empty;

    public IndexModel(IConfiguration config) => _config = config;

    public async Task OnGetAsync()
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Son başarılı işlem tarihi
            await using var cmd1 = new SqlCommand(
                "SELECT TOP 1 IslenmeTarihi FROM DosyaIslemeLog WHERE Durum = 'Basarili' ORDER BY IslenmeTarihi DESC",
                conn);
            var sonuc = await cmd1.ExecuteScalarAsync();
            if (sonuc is DateTime dt) SonGuncelleme = dt;

            // En son işlenen dosyanın tarih aralığı
            await using var cmd2 = new SqlCommand(
                "SELECT TOP 1 BasTarih, BitTarih FROM DosyaIslemeLog WHERE Durum = 'Basarili' ORDER BY IslenmeTarihi DESC",
                conn);
            await using var reader = await cmd2.ExecuteReaderAsync();
            if (await reader.ReadAsync() && !reader.IsDBNull(0))
            {
                SonBasTarih = reader.GetDateTime(0).ToString("yyyy-MM-ddTHH:mm");
                SonBitTarih = reader.GetDateTime(1).ToString("yyyy-MM-ddTHH:mm");
            }
        }
        catch { /* bağlantı hatası — sayfayı boş göster */ }
    }
}
