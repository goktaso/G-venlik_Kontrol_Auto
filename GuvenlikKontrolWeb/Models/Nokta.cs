using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuvenlikKontrol.Models
{
    public class Nokta
    {
        [Key]
        public int NoktaID { get; set; }

        public string NoktaAdi { get; set; }

        public string Aktif { get; set; }

        public DateTime GecerlilikBaslangic { get; set; }

        public DateTime? GecerlilikBitis { get; set; }
    }
}
