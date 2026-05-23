using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExcelDataReader;
using System.Data;
using GuvenlikKontrolWeb.Data;
using GuvenlikKontrolWeb.Models;
using System.IO;

namespace GuvenlikKontrolWeb.Pages
{
    public class VeriYukleModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public VeriYukleModel(ApplicationDbContext context) => _context = context;

        [BindProperty]
        public IFormFile ExcelDosyasi { get; set; }

        [BindProperty]
        public IFormFile LogoDosyasi { get; set; } // Yeni logo mülkiyeti

        public string BilgiMesaji { get; set; }

        // VARSAYILAN EXCEL YÜKLEME METODU
        public async Task<IActionResult> OnPostAsync()
        {
            if (ExcelDosyasi == null) return Page();

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            int eklenen = 0; int atlanan = 0;

            using (var stream = ExcelDosyasi.OpenReadStream())
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables[0];

                for (int i = 1; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    string bekci = row[1]?.ToString()?.Trim();
                    string nokta = row[2]?.ToString()?.Trim();
                    DateTime.TryParse(row[4]?.ToString(), out DateTime zaman);

                    bool varMi = _context.BekciKontrolKayitlari.Any(x => x.BekciAdi == bekci && x.DevriyeZamani == zaman);

                    if (!varMi && !string.IsNullOrEmpty(bekci))
                    {
                        _context.BekciKontrolKayitlari.Add(new BekciKontrolKayitlari
                        {
                            BekciAdi = bekci,
                            KontrolNoktasiAdi = nokta,
                            OkuyucuKodu = row[3]?.ToString(),
                            DevriyeZamani = zaman
                        });
                        eklenen++;
                    }
                    else { atlanan++; }
                }
                await _context.SaveChangesAsync();
            }
            BilgiMesaji = $"Ýþlem Tamam: {eklenen} yeni kayýt eklendi, {atlanan} kayýt zaten mevcuttu.";
            return Page();
        }

        // YENÝ LOGO YÜKLEME METODU (HANDLER)
        public async Task<IActionResult> OnPostLogoYukleAsync()
        {
            if (LogoDosyasi == null || LogoDosyasi.Length == 0)
            {
                BilgiMesaji = "Hata: Lütfen geçerli bir PNG logo seçin.";
                return Page();
            }

            // wwwroot/images/unlu-logo.png yolunu belirle
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "unlu-logo.png");

            // Dosyayý kaydet
            using (var stream = new FileStream(path, FileMode.Create))
            {
                await LogoDosyasi.CopyToAsync(stream);
            }

            BilgiMesaji = "Þirket logosu baþarýyla güncellendi!";
            return Page();
        }
    }
}