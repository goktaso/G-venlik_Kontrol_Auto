using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using System.Data;

namespace GuvenlikKontrolWeb.Pages.Account
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public string Username { get; set; }
        [BindProperty]
        public string Password { get; set; }
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {

            // SQL Authentication Baðlantý Dizesi
            string connectionString = @"Server=ARDA\ARDA;Database=GuvenlikKontrol1;User Id=data;Password=data123;TrustServerCertificate=True;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                // Resimdeki sütun isimlerine göre tam eþleþen sorgu:
                string sql = @"SELECT Rol, AdSoyad 
                   FROM dbo.SistemKullanicilari 
                   WHERE KullaniciAdi = @user 
                   AND Sifre = @pass 
                   AND AktifMi = 1"; // Sadece aktif kullanýcýlar girebilsin

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@user", Username);
                cmd.Parameters.AddWithValue("@pass", Password);

                try
                {
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string role = reader["Rol"].ToString(); // Admin, PowerUser, User
                            string fullName = reader["AdSoyad"]?.ToString() ?? Username;

                            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, Username),
                    new Claim("FullName", fullName),
                    new Claim(ClaimTypes.Role, role)
                };

                            var identity = new ClaimsIdentity(claims, "MyCookieAuth");
                            var principal = new ClaimsPrincipal(identity);

                            // Tarayýcýya giriþ biletini veriyoruz
                            await HttpContext.SignInAsync("MyCookieAuth", principal);

                            return RedirectToPage("/Index");
                        }
                        else
                        {
                            ErrorMessage = "Kullanýcý adý, þifre hatalý veya hesap pasif!";
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Veritabaný baðlantý hatasý: " + ex.Message;
                }
            }




            return Page();
        }
    }
}