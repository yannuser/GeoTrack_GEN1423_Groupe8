using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PositionsGps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehiculeId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Vitesse = table.Column<double>(type: "float", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Horodatage = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EtatVehicule = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NiveauCarburant = table.Column<double>(type: "float", nullable: true),
                    Erreur = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionsGps", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PositionsGps");
        }
    }
}
