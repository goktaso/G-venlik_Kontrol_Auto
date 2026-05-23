using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using GuvenlikKontrolWeb.Data;
using GuvenlikKontrolWeb.Services;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritaban�
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Razor Pages Ayarlar� (KATILA�TIRILDI)
builder.Services.AddRazorPages(options => {
    options.Conventions.AuthorizeFolder("/"); // Her yer kilitli
    options.Conventions.AllowAnonymousToPage("/Account/Login"); // Sadece Login a��k
});

// 3. Kimlik Do�rulama
builder.Services.AddAuthentication("MyCookieAuth")
    .AddCookie("MyCookieAuth", options => {
        options.Cookie.Name = "GuvenlikKontrol.Auth";
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true; // Hareket varsa s�reyi uzat
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 4. Middleware S�ralamas�
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// -----------------------------------------------------------
// 5. API U�LARI
// -----------------------------------------------------------

// TUR ANALİZ API
app.MapGet("/api/TurAnaliz", async (string baslangic, string bitis, IConfiguration config) =>
{
    var result = new List<object>();
    var connStr = config.GetConnectionString("DefaultConnection");
    try
    {
        using var conn = new SqlConnection(connStr);
        using var cmd = new SqlCommand("sp_TurAnalizi", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.Add("@BasTarih", SqlDbType.DateTime).Value = DateTime.Parse(baslangic);
        cmd.Parameters.Add("@BitTarih", SqlDbType.DateTime).Value = DateTime.Parse(bitis);
        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string tip  = reader["Tip"]?.ToString() ?? "";
            string sure = reader["Sure"]?.ToString() ?? "00:00:00";
            int sureSn  = TimeSpan.TryParse(sure, out var ts) ? (int)ts.TotalSeconds : 0;

            bool zamanNull = reader.IsDBNull(reader.GetOrdinal("DevriyeZamani"));

            result.Add(new
            {
                SatirTipi         = tip,
                TurIciNo          = reader["No"],
                BekciAdi          = reader["BekciAdi"]?.ToString(),
                KontrolNoktasiAdi = reader["KontrolNoktasiAdi"]?.ToString(),
                DevriyeZamani     = zamanNull ? (object?)null : reader.GetDateTime(reader.GetOrdinal("DevriyeZamani")),
                OperasyonGunu     = zamanNull ? (object?)null : reader.GetDateTime(reader.GetOrdinal("DevriyeZamani")),
                TurNo             = reader["TurNo"],
                IkiNoktaArasiSn   = tip == "DETAY"  ? sureSn : 0,
                TurToplamSn       = tip == "TOPLAM" ? sureSn : 0,
                IkiTurArasiSn     = 0,
                EksikAciklama     = reader["EksikAciklama"]?.ToString(),
                MukerrerAciklama  = reader["MukerrerAciklama"]?.ToString()
            });
        }
        return Results.Ok(result);
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
}).RequireAuthorization();

// BEK�� PERFORMANS API (YEN� EKLEND�)
app.MapGet("/api/BekciPerformans", async (string baslangic, string bitis, IConfiguration config) =>
{
    var result = new List<object>();
    var connStr = config.GetConnectionString("DefaultConnection");
    try
    {
        using var conn = new SqlConnection(connStr);
        // SQL'deki Stored Procedure isminin sp_BekciPerformansi oldu�undan emin ol
        using var cmd = new SqlCommand("sp_BekciPerformansi", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.Add("@BasTarih", SqlDbType.DateTime).Value = DateTime.Parse(baslangic);
        cmd.Parameters.Add("@BitTarih", SqlDbType.DateTime).Value = DateTime.Parse(bitis);
        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                BekciAdi = reader["BekciAdi"]?.ToString(),
                ToplamTur = reader["ToplamTur"],
                EksiksizTur = reader["EksiksizTur"],
                EksikliTur = reader["EksikliTur"],
                EksikNokta = reader["EksikNokta"],
                BasariOrani = reader["BasariOrani"]
            });
        }
        return Results.Ok(result);
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
}).RequireAuthorization();

// DB'deki en son tarih aralığını döner — Layout bunu kullanarak otomatik seçim yapar
app.MapGet("/api/SonTarihAraligi", async (IConfiguration config) =>
{
    var connStr = config.GetConnectionString("DefaultConnection");
    try
    {
        using var conn = new SqlConnection(connStr);
        using var cmd  = new SqlCommand(
            "SELECT TOP 1 BasTarih, BitTarih FROM DosyaIslemeLog WHERE Durum = 'Basarili' ORDER BY IslenmeTarihi DESC", conn);
        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync() && !reader.IsDBNull(0))
        {
            return Results.Ok(new {
                basTarih = reader.GetDateTime(0).ToString("yyyy-MM-ddTHH:mm"),
                bitTarih = reader.GetDateTime(1).ToString("yyyy-MM-ddTHH:mm")
            });
        }
        return Results.Ok(new { basTarih = (string?)null, bitTarih = (string?)null });
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
}).RequireAuthorization();

app.Run();