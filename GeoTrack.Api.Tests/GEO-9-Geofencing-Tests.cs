// ============================================================
// GEO-9 : tests unitaires du geofencing
//
// Couvre GeofencingService (calcul de distance, etat dedans/dehors, detection
// de la TRANSITION de sortie) et ZonesController (creation, liste filtree,
// suppression, protection [Authorize]).
//
// POURQUOI L'ALIAS `Modeles` : GEO-50-ZoneGeographique-Tests.cs declare une
// classe `ZoneGeographique` locale dans ce meme namespace GeoTrack.Api.Tests.
// Les types d'un namespace englobant l'emportant sur ceux importes par un
// using, `ZoneGeographique` sans prefixe designerait cette copie de test, sans
// aucun rapport avec l'entite persistee. L'alias de namespace ne peut pas etre
// masque : il garantit qu'on teste bien le modele de production.
//
// xUnit natif uniquement : aucune dependance a Moq ni FluentAssertions.
// ============================================================

using GeoTrack.Api.Controllers;
using GeoTrack.Api.Data;
using GeoTrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modeles = GeoTrack.Api.Models;

namespace GeoTrack.Api.Tests
{
    // =========================================================================
    // LOGIQUE DE DETECTION
    // =========================================================================

    public class GEO9_GeofencingServiceTests
    {
        private readonly GeofencingService _service = new();

        // Centre de reference : Parlement d'Ottawa.
        private const double CentreLat = 45.4236;
        private const double CentreLng = -75.7009;

        private static Modeles.ZoneGeographique ZoneDe(double rayonMetres) => new()
        {
            Id = 1,
            Nom = "Depot central",
            Latitude = CentreLat,
            Longitude = CentreLng,
            RayonMetres = rayonMetres,
            VehiculeId = "VEH-001",
            TypeAlerte = Modeles.TypeAlerteZone.SortieZone
        };

        private static Modeles.PositionGps PositionA(double lat, double lng) => new()
        {
            VehiculeId = "VEH-001",
            Latitude = lat,
            Longitude = lng,
            Horodatage = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc),
            EtatVehicule = "en_route"
        };

        // ---------------------------------------------------------------
        // Distance (Haversine)
        // ---------------------------------------------------------------

        [Fact]
        public void Distance_EstNulle_EntreUnPointEtLuiMeme()
        {
            var distance = _service.CalculerDistanceMetres(
                CentreLat, CentreLng, CentreLat, CentreLng);

            Assert.Equal(0, distance, precision: 6);
        }

        [Fact]
        public void Distance_EstSymetrique()
        {
            var aller = _service.CalculerDistanceMetres(45.4236, -75.7009, 45.5017, -73.5673);
            var retour = _service.CalculerDistanceMetres(45.5017, -73.5673, 45.4236, -75.7009);

            Assert.Equal(aller, retour, precision: 6);
        }

        [Fact]
        public void Distance_OttawaMontreal_EstDeLOrdreDe160Km()
        {
            // Reference connue : environ 166 km a vol d'oiseau. Une tolerance de
            // 5 km suffit a detecter une erreur de formule ou d'unite, sans
            // rendre le test fragile.
            var distance = _service.CalculerDistanceMetres(45.4236, -75.7009, 45.5017, -73.5673);

            Assert.InRange(distance, 161_000, 171_000);
        }

        // ---------------------------------------------------------------
        // Etat instantane
        // ---------------------------------------------------------------

        [Fact]
        public void Etat_VehiculeAuCentre_EstInterieur()
        {
            var etat = _service.DeterminerEtat(ZoneDe(500), CentreLat, CentreLng);

            Assert.Equal(EtatZone.Interieur, etat);
        }

        [Fact]
        public void Etat_VehiculeTresLoin_EstExterieur()
        {
            // Montreal, pour une zone de 500 m autour d'Ottawa.
            var etat = _service.DeterminerEtat(ZoneDe(500), 45.5017, -73.5673);

            Assert.Equal(EtatZone.Exterieur, etat);
        }

        [Fact]
        public void Etat_SurLaBordure_EstConsidereInterieur()
        {
            // Une zone dont le rayon vaut exactement la distance mesuree : la
            // comparaison etant <=, le point sur le cercle est dedans.
            var distance = _service.CalculerDistanceMetres(
                CentreLat, CentreLng, CentreLat + 0.01, CentreLng);

            var zone = ZoneDe(distance);

            Assert.Equal(EtatZone.Interieur,
                _service.DeterminerEtat(zone, CentreLat + 0.01, CentreLng));
        }

        // ---------------------------------------------------------------
        // Detection de transition : le coeur de GEO-9
        // ---------------------------------------------------------------

        [Fact]
        public void Sortie_EstDetectee_SurLaTransitionDedansVersDehors()
        {
            var zone = ZoneDe(500);
            var precedente = PositionA(CentreLat, CentreLng);          // dedans
            var actuelle = PositionA(45.5017, -73.5673);               // dehors

            var evaluations = _service.Evaluer(new[] { zone }, actuelle, precedente);

            var evaluation = Assert.Single(evaluations);
            Assert.Equal(EtatZone.Interieur, evaluation.EtatPrecedent);
            Assert.Equal(EtatZone.Exterieur, evaluation.EtatActuel);
            Assert.True(evaluation.SortieDetectee);
        }

        [Fact]
        public void Sortie_NEstPasRedeclenchee_SiDejaDehorsAuTickPrecedent()
        {
            // Le scenario que GEO-9 doit eviter : un vehicule sorti depuis
            // longtemps ne doit pas generer une alerte a chaque position recue.
            var zone = ZoneDe(500);
            var precedente = PositionA(45.5017, -73.5673);             // deja dehors
            var actuelle = PositionA(45.5020, -73.5680);               // toujours dehors

            var evaluations = _service.Evaluer(new[] { zone }, actuelle, precedente);

            var evaluation = Assert.Single(evaluations);
            Assert.Equal(EtatZone.Exterieur, evaluation.EtatPrecedent);
            Assert.Equal(EtatZone.Exterieur, evaluation.EtatActuel);
            Assert.False(evaluation.SortieDetectee);
        }

        [Fact]
        public void Sortie_NEstPasDeclenchee_QuandLeVehiculeResteDedans()
        {
            var zone = ZoneDe(5_000);
            var precedente = PositionA(CentreLat, CentreLng);
            var actuelle = PositionA(CentreLat + 0.001, CentreLng);

            var evaluations = _service.Evaluer(new[] { zone }, actuelle, precedente);

            var evaluation = Assert.Single(evaluations);
            Assert.Equal(EtatZone.Interieur, evaluation.EtatActuel);
            Assert.False(evaluation.SortieDetectee);
        }

        [Fact]
        public void Sortie_NEstPasDeclenchee_ALaToutePremierePosition()
        {
            // Sans position anterieure, on ignore d'ou vient le vehicule : on ne
            // peut donc pas affirmer qu'il vient de sortir.
            var zone = ZoneDe(500);
            var actuelle = PositionA(45.5017, -73.5673);               // dehors

            var evaluations = _service.Evaluer(new[] { zone }, actuelle, positionPrecedente: null);

            var evaluation = Assert.Single(evaluations);
            Assert.Equal(EtatZone.Inconnu, evaluation.EtatPrecedent);
            Assert.Equal(EtatZone.Exterieur, evaluation.EtatActuel);
            Assert.False(evaluation.SortieDetectee);
        }

        [Fact]
        public void Retour_DansLaZone_NeDeclencheAucuneSortie()
        {
            var zone = ZoneDe(500);
            var precedente = PositionA(45.5017, -73.5673);             // dehors
            var actuelle = PositionA(CentreLat, CentreLng);            // rentre

            var evaluations = _service.Evaluer(new[] { zone }, actuelle, precedente);

            var evaluation = Assert.Single(evaluations);
            Assert.Equal(EtatZone.Exterieur, evaluation.EtatPrecedent);
            Assert.Equal(EtatZone.Interieur, evaluation.EtatActuel);
            Assert.False(evaluation.SortieDetectee);
        }

        [Fact]
        public void Evaluer_TraiteChaqueZoneIndependamment()
        {
            // Le vehicule sort de la petite zone mais reste dans la grande :
            // une seule sortie doit etre signalee.
            var petite = ZoneDe(500);
            var grande = ZoneDe(500_000);
            grande.Id = 2;
            grande.Nom = "Region";

            var precedente = PositionA(CentreLat, CentreLng);
            var actuelle = PositionA(45.5017, -73.5673);

            var evaluations = _service.Evaluer(new[] { petite, grande }, actuelle, precedente);

            Assert.Equal(2, evaluations.Count);
            Assert.True(evaluations.Single(e => e.Zone.Id == 1).SortieDetectee);
            Assert.False(evaluations.Single(e => e.Zone.Id == 2).SortieDetectee);
        }

        [Fact]
        public void Evaluer_SansZone_NeRenvoieRien()
        {
            var evaluations = _service.Evaluer(
                Array.Empty<Modeles.ZoneGeographique>(),
                PositionA(CentreLat, CentreLng),
                null);

            Assert.Empty(evaluations);
        }

        [Fact]
        public void Evaluer_ReporteLaDistanceCalculee()
        {
            var zone = ZoneDe(500);
            var actuelle = PositionA(45.5017, -73.5673);

            var evaluation = Assert.Single(_service.Evaluer(new[] { zone }, actuelle, null));

            Assert.InRange(evaluation.DistanceMetres, 161_000, 171_000);
        }
    }

    // =========================================================================
    // ENDPOINTS
    // =========================================================================

    public class GEO9_ZonesControllerTests
    {
        private static GeoTrackContext CreerContexteEnMemoire()
        {
            var options = new DbContextOptionsBuilder<GeoTrackContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new GeoTrackContext(options);
        }

        private static CreerZoneRequest RequeteValide() => new()
        {
            Nom = "Depot central",
            Latitude = 45.4236,
            Longitude = -75.7009,
            RayonMetres = 500,
            VehiculeId = "VEH-001"
        };

        // ---------------------------------------------------------------
        // Protection
        // ---------------------------------------------------------------

        [Fact]
        public void Controleur_EstProtegePar_Authorize()
        {
            // Le pipeline d'authentification ne tourne pas en test unitaire :
            // on verifie donc la presence de l'attribut sur le type, ce qui
            // suffit a detecter une suppression accidentelle.
            var attributs = typeof(ZonesController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);

            Assert.NotEmpty(attributs);
        }

        // ---------------------------------------------------------------
        // Creation
        // ---------------------------------------------------------------

        [Fact]
        public async Task Post_CreeLaZone_QuandLaRequeteEstValide()
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            var resultat = await controleur.Creer(RequeteValide());

            var cree = Assert.IsType<CreatedAtActionResult>(resultat);
            Assert.Equal(201, cree.StatusCode);

            var zone = Assert.IsType<Modeles.ZoneGeographique>(cree.Value);
            Assert.True(zone.Id > 0);
            Assert.Equal("Depot central", zone.Nom);
            Assert.Equal(Modeles.TypeAlerteZone.SortieZone, zone.TypeAlerte);

            Assert.Single(context.ZonesGeographiques);
        }

        [Fact]
        public async Task Post_RefuseUnCorpsVide()
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            var resultat = await controleur.Creer(null);

            Assert.IsType<BadRequestObjectResult>(resultat);
            Assert.Empty(context.ZonesGeographiques);
        }

        [Fact]
        public async Task Post_RefuseUnNomManquant()
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            var requete = RequeteValide();
            requete.Nom = "   ";

            var resultat = await controleur.Creer(requete);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultat);
            Assert.Contains("Nom", Assert.IsType<string>(badRequest.Value));
            Assert.Empty(context.ZonesGeographiques);
        }

        [Fact]
        public async Task Post_RefuseUnVehiculeIdManquant()
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            var requete = RequeteValide();
            requete.VehiculeId = null;

            var resultat = await controleur.Creer(requete);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultat);
            Assert.Contains("VehiculeId", Assert.IsType<string>(badRequest.Value));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Post_RefuseUnRayonNonPositif(double rayon)
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            var requete = RequeteValide();
            requete.RayonMetres = rayon;

            var resultat = await controleur.Creer(requete);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultat);
            Assert.Contains("RayonMetres", Assert.IsType<string>(badRequest.Value));
        }

        [Theory]
        [InlineData(91, -75)]
        [InlineData(-91, -75)]
        [InlineData(45, 181)]
        [InlineData(45, -181)]
        public async Task Post_RefuseDesCoordonneesHorsBornes(double lat, double lng)
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            var requete = RequeteValide();
            requete.Latitude = lat;
            requete.Longitude = lng;

            var resultat = await controleur.Creer(requete);

            Assert.IsType<BadRequestObjectResult>(resultat);
            Assert.Empty(context.ZonesGeographiques);
        }

        // ---------------------------------------------------------------
        // Liste
        // ---------------------------------------------------------------

        [Fact]
        public async Task Get_ListeToutesLesZones_SansFiltre()
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            await controleur.Creer(RequeteValide());

            var autre = RequeteValide();
            autre.Nom = "Chantier nord";
            autre.VehiculeId = "VEH-002";
            await controleur.Creer(autre);

            var resultat = await controleur.Lister();

            var ok = Assert.IsType<OkObjectResult>(resultat.Result);
            var zones = Assert.IsAssignableFrom<IEnumerable<Modeles.ZoneGeographique>>(ok.Value);
            Assert.Equal(2, zones.Count());
        }

        [Fact]
        public async Task Get_FiltreParVehicule()
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            await controleur.Creer(RequeteValide());

            var autre = RequeteValide();
            autre.Nom = "Chantier nord";
            autre.VehiculeId = "VEH-002";
            await controleur.Creer(autre);

            var resultat = await controleur.Lister("VEH-002");

            var ok = Assert.IsType<OkObjectResult>(resultat.Result);
            var zones = Assert.IsAssignableFrom<IEnumerable<Modeles.ZoneGeographique>>(ok.Value);

            var zone = Assert.Single(zones);
            Assert.Equal("Chantier nord", zone.Nom);
        }

        [Fact]
        public async Task Get_FiltreInconnu_RenvoieUneListeVide()
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            await controleur.Creer(RequeteValide());

            var resultat = await controleur.Lister("VEH-INEXISTANT");

            var ok = Assert.IsType<OkObjectResult>(resultat.Result);
            var zones = Assert.IsAssignableFrom<IEnumerable<Modeles.ZoneGeographique>>(ok.Value);
            Assert.Empty(zones);
        }

        // ---------------------------------------------------------------
        // Suppression
        // ---------------------------------------------------------------

        [Fact]
        public async Task Delete_SupprimeLaZone()
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            var cree = (CreatedAtActionResult)await controleur.Creer(RequeteValide());
            var zone = (Modeles.ZoneGeographique)cree.Value!;

            var resultat = await controleur.Supprimer(zone.Id);

            Assert.IsType<NoContentResult>(resultat);
            Assert.Empty(context.ZonesGeographiques);
        }

        [Fact]
        public async Task Delete_ZoneInexistante_RenvoieNotFound()
        {
            using var context = CreerContexteEnMemoire();
            var controleur = new ZonesController(context);

            var resultat = await controleur.Supprimer(999);

            Assert.IsType<NotFoundObjectResult>(resultat);
        }
    }

    // =========================================================================
    // INTEGRATION : reception GPS -> declenchement d'alerte
    // =========================================================================

    public class GEO9_IntegrationPositionGpsTests
    {
        private static GeoTrackContext CreerContexteEnMemoire()
        {
            var options = new DbContextOptionsBuilder<GeoTrackContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new GeoTrackContext(options);
        }

        private sealed class StubNotificateur : INotificateurAlerteZone
        {
            public List<AlerteSortieZone> Alertes { get; } = new();

            public Task SignalerSortieDeZoneAsync(AlerteSortieZone alerte)
            {
                Alertes.Add(alerte);
                return Task.CompletedTask;
            }
        }

        private static Modeles.PositionGps PositionA(double lat, double lng, DateTime horodatage) => new()
        {
            VehiculeId = "VEH-001",
            Latitude = lat,
            Longitude = lng,
            Horodatage = horodatage,
            EtatVehicule = "en_route"
        };

        private static async Task AjouterZoneAsync(GeoTrackContext context)
        {
            context.ZonesGeographiques.Add(new Modeles.ZoneGeographique
            {
                Nom = "Depot central",
                Latitude = 45.4236,
                Longitude = -75.7009,
                RayonMetres = 500,
                VehiculeId = "VEH-001"
            });
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task Position_DeclencheUneAlerte_QuandLeVehiculeQuitteLaZone()
        {
            using var context = CreerContexteEnMemoire();
            await AjouterZoneAsync(context);

            var notificateur = new StubNotificateur();
            var controleur = FabriqueControleurPositions.Creer(context, notificateurZone: notificateur);

            var depart = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

            // 1re position : dans la zone. Aucune alerte (pas d'antecedent).
            await controleur.Recevoir(PositionA(45.4236, -75.7009, depart));
            Assert.Empty(notificateur.Alertes);

            // 2e position : hors zone -> transition detectee.
            await controleur.Recevoir(PositionA(45.5017, -73.5673, depart.AddMinutes(5)));

            var alerte = Assert.Single(notificateur.Alertes);
            Assert.Equal("VEH-001", alerte.VehiculeId);
            Assert.Equal("Depot central", alerte.NomZone);
            Assert.Equal(500, alerte.RayonMetres);
            Assert.True(alerte.DistanceMetres > 500);
        }

        [Fact]
        public async Task Position_NeRedeclenchePas_SiLeVehiculeEstDejaDehors()
        {
            using var context = CreerContexteEnMemoire();
            await AjouterZoneAsync(context);

            var notificateur = new StubNotificateur();
            var controleur = FabriqueControleurPositions.Creer(context, notificateurZone: notificateur);

            var depart = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

            await controleur.Recevoir(PositionA(45.4236, -75.7009, depart));               // dedans
            await controleur.Recevoir(PositionA(45.5017, -73.5673, depart.AddMinutes(5))); // sortie
            await controleur.Recevoir(PositionA(45.5020, -73.5680, depart.AddMinutes(10))); // toujours dehors
            await controleur.Recevoir(PositionA(45.5030, -73.5690, depart.AddMinutes(15))); // toujours dehors

            // Une seule alerte, malgre trois positions hors zone.
            Assert.Single(notificateur.Alertes);
        }

        [Fact]
        public async Task Position_SansZoneConfiguree_NeDeclencheRien()
        {
            using var context = CreerContexteEnMemoire();

            var notificateur = new StubNotificateur();
            var controleur = FabriqueControleurPositions.Creer(context, notificateurZone: notificateur);

            var depart = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

            await controleur.Recevoir(PositionA(45.4236, -75.7009, depart));
            await controleur.Recevoir(PositionA(45.5017, -73.5673, depart.AddMinutes(5)));

            Assert.Empty(notificateur.Alertes);
            Assert.Equal(2, await context.PositionsGps.CountAsync());
        }

        [Fact]
        public async Task Position_IgnoreLesZonesDUnAutreVehicule()
        {
            using var context = CreerContexteEnMemoire();

            context.ZonesGeographiques.Add(new Modeles.ZoneGeographique
            {
                Nom = "Zone d'un autre",
                Latitude = 45.4236,
                Longitude = -75.7009,
                RayonMetres = 500,
                VehiculeId = "VEH-999"
            });
            await context.SaveChangesAsync();

            var notificateur = new StubNotificateur();
            var controleur = FabriqueControleurPositions.Creer(context, notificateurZone: notificateur);

            var depart = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

            await controleur.Recevoir(PositionA(45.4236, -75.7009, depart));
            await controleur.Recevoir(PositionA(45.5017, -73.5673, depart.AddMinutes(5)));

            Assert.Empty(notificateur.Alertes);
        }

        [Fact]
        public async Task Position_Rejetee_NEnregistreNiPositionNiAlerte()
        {
            using var context = CreerContexteEnMemoire();
            await AjouterZoneAsync(context);

            var notificateur = new StubNotificateur();
            var controleur = FabriqueControleurPositions.Creer(context, notificateurZone: notificateur);

            var invalide = PositionA(45.4236, -75.7009, new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));
            invalide.VehiculeId = string.Empty;

            var resultat = await controleur.Recevoir(invalide);

            Assert.IsType<BadRequestObjectResult>(resultat);
            Assert.Empty(context.PositionsGps);
            Assert.Empty(notificateur.Alertes);
        }
    }
}
