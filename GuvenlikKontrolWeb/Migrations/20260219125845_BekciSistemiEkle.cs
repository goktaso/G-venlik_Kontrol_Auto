using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuvenlikKontrolWeb.Migrations
{
    /// <inheritdoc />
    public partial class BekciSistemiEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BekciKontrolKayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BekciAdi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KontrolNoktasiAdi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OkuyucuKodu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DevriyeZamani = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BekciKontrolKayitlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Noktalar",
                columns: table => new
                {
                    NoktaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NoktaAdi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Aktif = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GecerlilikBaslangic = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GecerlilikBitis = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Noktalar", x => x.NoktaID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BekciKontrolKayitlari");

            migrationBuilder.DropTable(
                name: "Noktalar");
        }
    }
}
