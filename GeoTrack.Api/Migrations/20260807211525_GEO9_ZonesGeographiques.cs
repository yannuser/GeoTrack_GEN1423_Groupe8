using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class GEO9_ZonesGeographiques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZonesGeographiques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    RayonMetres = table.Column<double>(type: "float", nullable: false),
                    VehiculeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TypeAlerte = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZonesGeographiques", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZonesGeographiques_VehiculeId",
                table: "ZonesGeographiques",
                column: "VehiculeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZonesGeographiques");
        }
    }
}
