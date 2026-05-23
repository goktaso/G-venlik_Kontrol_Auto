using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace GuvenlikKontrolWeb.Pages
{
    public class BekciPerformansModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public BekciPerformansModel(IConfiguration configuration) => _configuration = configuration;

        public void OnGet() { }

        public JsonResult OnGetPerformansVerisi(string baslangic, string bitis)
        {
            var dataList = new List<object>();
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    // SQL'deki prosedür adýnýn doðruluðundan emin ol abi
                    using (SqlCommand cmd = new SqlCommand("sp_BekciPerformans", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@BasTarih", DateTime.Parse(baslangic));
                        cmd.Parameters.AddWithValue("@BitTarih", DateTime.Parse(bitis));

                        conn.Open();
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                dataList.Add(new
                                {
                                    BekciAdi = rdr["BekciAdi"]?.ToString(),
                                    ToplamTur = Convert.ToInt32(rdr["ToplamTur"]),
                                    EksiksizTur = Convert.ToInt32(rdr["EksiksizTur"]),
                                    EksikliTur = Convert.ToInt32(rdr["EksikliTur"]),
                                    EksikNokta = Convert.ToInt32(rdr["EksikNokta"]),
                                    BasariOrani = Convert.ToDouble(rdr["BasariOrani"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message });
            }

            // PropertyNamingPolicy = null: BekciAdi -> bekciAdi dönüþümünü engeller
            return new JsonResult(dataList, new JsonSerializerOptions { PropertyNamingPolicy = null });
        }
    }
}