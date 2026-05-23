using GuvenlikKontrol.Models;
using GuvenlikKontrolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace GuvenlikKontrolWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Bu Constructor (Yapıcı Metot) OLMALIDIR, hata bunu istiyor:
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Nokta> Noktalar { get; set; }
        public DbSet<BekciKontrolKayitlari> BekciKontrolKayitlari { get; set; }
    }
}