-- G.Kontrol_Automation veritabanında çalıştırın
-- Worker'ın log kayıtlarını yazacağı tablo

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'DosyaIslemeLog'
)
BEGIN
    CREATE TABLE DosyaIslemeLog (
        Id            INT           IDENTITY(1,1) PRIMARY KEY,
        DosyaAdi      NVARCHAR(500) NOT NULL,
        IslenmeTarihi DATETIME      NOT NULL DEFAULT GETDATE(),
        EklenenKayit  INT           NOT NULL DEFAULT 0,
        AtlananKayit  INT           NOT NULL DEFAULT 0,
        BasTarih      DATETIME      NULL,
        BitTarih      DATETIME      NULL,
        Durum         NVARCHAR(20)  NOT NULL,   -- 'Basarili' | 'Hata'
        HataMesaji    NVARCHAR(MAX) NULL
    );

    PRINT 'DosyaIslemeLog tablosu oluşturuldu.';
END
ELSE
BEGIN
    PRINT 'DosyaIslemeLog tablosu zaten mevcut.';
END
