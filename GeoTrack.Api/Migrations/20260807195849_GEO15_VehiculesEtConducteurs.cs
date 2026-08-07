using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class GEO15_VehiculesEtConducteurs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Conducteurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conducteurs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Immatriculation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    VIN = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: true),
                    Marque = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Modele = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    TrackerGpsId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FournisseurGps = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    StatutGps = table.Column<int>(type: "int", nullable: false),
                    ConducteurId = table.Column<int>(type: "int", nullable: true),
                    ConducteurNom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupeDivision = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VitesseMaxKmh = table.Column<double>(type: "float", nullable: true),
                    ZoneParDefautId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PremiereLat = table.Column<double>(type: "float", nullable: true),
                    PremiereLng = table.Column<double>(type: "float", nullable: true),
                    PremierePositionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicules_Immatriculation",
                table: "Vehicules",
                column: "Immatriculation",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicules_TrackerGpsId",
                table: "Vehicules",
                column: "TrackerGpsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicules_VIN",
                table: "Vehicules",
                column: "VIN",
                unique: true,
                filter: "[VIN] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Conducteurs");

            migrationBuilder.DropTable(
                name: "Vehicules");
        }
    }
}
