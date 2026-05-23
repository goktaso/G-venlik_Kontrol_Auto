using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuvenlikKontrolWeb.Models
{
    [Table("BekciKontrolKayitlari")] // SQL'deki gerçek tabloya buradan bağlanıyoruz
    public class TurVerisi
    {
        [Key]
        public int Id { get; set; }
        public int? TurNo { get; set; }
        public string? BekciAdi { get; set; }
        public DateTime? DevriyeZamani { get; set; }
        public string? KontrolNoktasiAdi { get; set; }
        public string? SatirTipi { get; set; }
        public int? TurIciNo { get; set; }
        public DateTime? OperasyonGunu { get; set; }
        public int? IkiNoktaArasiSn { get; set; }
        public int? TurToplamSn { get; set; }
        public int? IkiTurArasiSn { get; set; }
    }
}