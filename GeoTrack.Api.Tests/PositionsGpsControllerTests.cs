using GeoTrack.Api.Controllers;
using GeoTrack.Api.Data;
using GeoTrack.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoTrack.Api.Tests
{
    public class PositionsGpsControllerTests
    {
        // Chaque test recoit sa propre base en memoire, isolee des autres.
        private static GeoTrackContext CreerContexteEnMemoire()
        {
            var options = new DbContextOptionsBuilder<GeoTrackContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new GeoTrackContext(options);
        }

        private static PositionGps CreerPositionValide() => new()
        {
            VehiculeId = "VEH-001",
            Latitude = 45.5017,
            Longitude = -73.5673,
            Vitesse = 62.5,
            Direction = 180,
            Horodatage = new DateTime(2026, 8, 4, 12, 30, 0, DateTimeKind.Utc),
            EtatVehicule = "en_route",
            NiveauCarburant = 78.2
        };

        [Fact]
        public async Task Post_RetourneBadRequest_QuandVehiculeIdEstVide()
        {
            using var context = CreerContexteEnMemoire();
            var controller = new PositionsGpsController(context);

            var position = CreerPositionValide();
            position.VehiculeId = string.Empty;

            var resultat = await controller.Recevoir(position);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultat);
            Assert.Equal(400, badRequest.StatusCode);
            Assert.Empty(context.PositionsGps);
        }

        [Fact]
        public async Task Post_RetourneOk_QuandLaPositionEstValide()
        {
            using var context = CreerContexteEnMemoire();
            var controller = new PositionsGpsController(context);

            var position = CreerPositionValide();

            var resultat = await controller.Recevoir(position);

            var ok = Assert.IsType<OkObjectResult>(resultat);
            Assert.Equal(200, ok.StatusCode);

            var enregistree = Assert.IsType<PositionGps>(ok.Value);
            Assert.Equal("VEH-001", enregistree.VehiculeId);

            // La position doit reellement avoir ete persistee.
            Assert.Single(context.PositionsGps);
        }

        [Fact]
        public async Task Post_RetourneBadRequest_QuandHorodatageEstManquant()
        {
            using var context = CreerContexteEnMemoire();
            var controller = new PositionsGpsController(context);

            var position = CreerPositionValide();
            position.Horodatage = default;

            var resultat = await controller.Recevoir(position);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultat);
            Assert.Equal(400, badRequest.StatusCode);

            // Le message doit designer explicitement le champ fautif.
            var message = Assert.IsType<string>(badRequest.Value);
            Assert.Contains("Horodatage", message);

            // Une position sans date ne doit jamais etre acceptee silencieusement.
            Assert.Empty(context.PositionsGps);
        }

        [Fact]
        public async Task Get_RetourneLesPositions_TrieesParHorodatageDecroissant()
        {
            using var context = CreerContexteEnMemoire();
            var controller = new PositionsGpsController(context);

            var maintenant = new DateTime(2026, 8, 4, 12, 30, 0, DateTimeKind.Utc);

            var ancienne = CreerPositionValide();
            ancienne.VehiculeId = "VEH-ANCIENNE";
            ancienne.Horodatage = maintenant.AddMinutes(-10);

            var intermediaire = CreerPositionValide();
            intermediaire.VehiculeId = "VEH-INTERMEDIAIRE";
            intermediaire.Horodatage = maintenant.AddMinutes(-5);

            var recente = CreerPositionValide();
            recente.VehiculeId = "VEH-RECENTE";
            recente.Horodatage = maintenant.AddMinutes(-1);

            // Insertion dans le desordre, pour que le tri soit reellement teste.
            context.PositionsGps.AddRange(intermediaire, ancienne, recente);
            await context.SaveChangesAsync();

            var resultat = await controller.Dernieres();

            var ok = Assert.IsType<OkObjectResult>(resultat.Result);
            Assert.Equal(200, ok.StatusCode);

            var positions = Assert.IsType<List<PositionGps>>(ok.Value);
            Assert.Equal(3, positions.Count);
            Assert.Equal(
                new[] { "VEH-RECENTE", "VEH-INTERMEDIAIRE", "VEH-ANCIENNE" },
                positions.Select(p => p.VehiculeId).ToArray());
        }
    }
}
