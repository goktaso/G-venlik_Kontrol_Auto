using System;
using System.ComponentModel.DataAnnotations;

namespace GuvenlikKontrolWeb.Models
{
    public class BekciKontrolKayitlari
    {
        [Key]
        public int Id { get; set; }
        public string BekciAdi { get; set; }
        public string KontrolNoktasiAdi { get; set; }
        public string OkuyucuKodu { get; set; }
        public DateTime DevriyeZamani { get; set; }
    }
}