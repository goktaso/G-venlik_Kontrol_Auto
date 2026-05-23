-- ============================================================
-- Güvenlik Kontrol Sistemi — Veritabanı Kurulum Scripti
-- SSMS'de admin yetkisiyle çalıştırın
-- ============================================================

USE master;
GO

-- 1. Veritabanı oluştur (yoksa)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'G.Kontrol_Automation')
BEGIN
    CREATE DATABASE [G.Kontrol_Automation]
        COLLATE Turkish_CI_AS;
    PRINT '>> Veritabanı oluşturuldu: G.Kontrol_Automation';
END
ELSE
    PRINT '>> Veritabanı zaten mevcut, atlanıyor.';
GO

USE [G.Kontrol_Automation];
GO

-- ============================================================
-- 2. BekciKontrolKayitlari — Ham RFID okuma kayıtları
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[BekciKontrolKayitlari]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[BekciKontrolKayitlari] (
        [Id]                INT           IDENTITY(1,1) NOT NULL,
        [BekciAdi]          NVARCHAR(100) NOT NULL,
        [KontrolNoktasiAdi] NVARCHAR(200) NOT NULL,
        [OkuyucuKodu]       NVARCHAR(50)  NULL,
        [DevriyeZamani]     DATETIME      NOT NULL,
        CONSTRAINT [PK_BekciKontrolKayitlari] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Hız için index'ler
    CREATE INDEX [IX_BKK_Zaman]
        ON [dbo].[BekciKontrolKayitlari] ([DevriyeZamani] DESC);

    CREATE INDEX [IX_BKK_Unique_Kontrol]
        ON [dbo].[BekciKontrolKayitlari] ([BekciAdi], [KontrolNoktasiAdi], [DevriyeZamani]);

    PRINT '>> Tablo oluşturuldu: BekciKontrolKayitlari';
END
ELSE
    PRINT '>> BekciKontrolKayitlari zaten mevcut, atlanıyor.';
GO

-- ============================================================
-- 3. DosyaIslemeLog — Worker işlem geçmişi (web "Son Güncelleme" için)
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[DosyaIslemeLog]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[DosyaIslemeLog] (
        [Id]            INT            IDENTITY(1,1) NOT NULL,
        [DosyaAdi]      NVARCHAR(200)  NOT NULL,
        [IslenmeTarihi] DATETIME       NOT NULL,
        [EklenenKayit]  INT            NOT NULL DEFAULT 0,
        [AtlananKayit]  INT            NOT NULL DEFAULT 0,
        [BasTarih]      DATETIME       NULL,
        [BitTarih]      DATETIME       NULL,
        [Durum]         NVARCHAR(20)   NOT NULL,   -- 'Basarili' | 'Hata'
        [HataMesaji]    NVARCHAR(MAX)  NULL,
        CONSTRAINT [PK_DosyaIslemeLog] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_DIL_IslenmeTarihi]
        ON [dbo].[DosyaIslemeLog] ([IslenmeTarihi] DESC);

    PRINT '>> Tablo oluşturuldu: DosyaIslemeLog';
END
ELSE
    PRINT '>> DosyaIslemeLog zaten mevcut, atlanıyor.';
GO

-- ============================================================
-- 4. sp_TurAnalizi — KAYNAK VERİTABANINDAN AKTARILACAK
--    Mevcut SQL Server'dan dışa aktarmak için:
--    SSMS > G.Kontrol_Automation > Programmability > Stored Procedures
--    > sp_TurAnalizi > Sağ tık > Script Stored Procedure As > CREATE To > New Query Window
--    Oluşan scripti bu dosyanın altına ekleyin.
-- ============================================================

-- ============================================================
-- 5. sp_BekciPerformansi — KAYNAK VERİTABANINDAN AKTARILACAK
--    (sp_TurAnalizi ile aynı yöntem)
-- ============================================================

PRINT '';
PRINT '=== KURULUM TAMAMLANDI ===';
PRINT 'Kalan adım: sp_TurAnalizi ve sp_BekciPerformansi stored procedure''larini';
PRINT 'kaynak sunucudan script olarak alip bu veritabanina yukleyin.';
GO
