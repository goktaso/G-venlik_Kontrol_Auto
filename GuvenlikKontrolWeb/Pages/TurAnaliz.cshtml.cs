using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace GuvenlikKontrolWeb.Pages
{
    public class TurAnalizModel : PageModel
    {
        private readonly IConfiguration _config;
        public TurAnalizModel(IConfiguration config)
        {
            _config = config;
        }

        public List<TurAnalizItem> TurAnalizData { get; set; } = new List<TurAnalizItem>();

        [BindProperty(SupportsGet = true)]
        public string Baslangic { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Bitis { get; set; }

        public async Task OnGetAsync()
        {
            if (string.IsNullOrEmpty(Baslangic) || string.IsNullOrEmpty(Bitis))
                return;

            var connStr = _config.GetConnectionString("DefaultConnection");

            try
            {
                using var conn = new SqlConnection(connStr);
                using var cmd = new SqlCommand("sp_TurAnalizi", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@BasTarih", DateTime.Parse(Baslangic));
                cmd.Parameters.AddWithValue("@BitTarih", DateTime.Parse(Bitis));

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    TurAnalizData.Add(new TurAnalizItem
                    {
                        OperasyonGunu = reader.GetDateTime(0),
                        BekciAdi = reader.GetString(1),
                        KontrolNoktasiAdi = reader.GetString(2),
                        OkuyucuKodu = reader.GetValue(3)?.ToString() ?? "",
                        DevriyeZamani = reader.GetDateTime(4)
                    });
                }
            }
            catch
            {
                // Hata varsa TurAnalizData boþ kalýr
            }
        }
    }

    public class TurAnalizItem
    {
        public DateTime OperasyonGunu { get; set; }
        public string BekciAdi { get; set; }
        public string KontrolNoktasiAdi { get; set; }
        public string OkuyucuKodu { get; set; }
        public DateTime DevriyeZamani { get; set; }
    }
}

