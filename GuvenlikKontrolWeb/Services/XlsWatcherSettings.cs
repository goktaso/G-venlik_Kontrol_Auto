namespace GuvenlikKontrolWeb.Services
{
    public class XlsWatcherSettings
    {
        public string IzlenenKlasor { get; set; } = string.Empty;
        public string IslenmisMiKlasor { get; set; } = string.Empty;
        public int DosyaBeklemeSuresiMs { get; set; } = 2000;
    }
}
