using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class GEO58_AlertesCentralisees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alertes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VehiculeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TypeAlerte = table.Column<int>(type: "int", nullable: false),
                    Severite = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alertes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alertes_Date",
                table: "Alertes",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Alertes_VehiculeId_Date",
                table: "Alertes",
                columns: new[] { "VehiculeId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alertes");
        }
    }
}
