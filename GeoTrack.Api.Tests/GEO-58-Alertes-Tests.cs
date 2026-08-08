// ============================================================
// GEO-58 : tests de la table d'alertes centralisee
//
// Couvre les deux producteurs (vitesse GEO-51, sortie de zone GEO-9), leur
// persistance dans la table Alertes, la degradation gracieuse de chacun, et
// l'endpoint GET /api/alertes.
//
// Contient aussi FabriqueControleurPositions, fabrique partagee reutilisee par
// PositionsGpsControllerTests et GEO-9-Geofencing-Tests : le constructeur de
// PositionsGpsController s'enrichit a chaque chantier, un seul point de
// construction evite d'avoir a corriger trois fichiers a chaque fois.
//
// POURQUOI L'ALIAS `Modeles` : GEO-50-ZoneGeographique-Tests.cs declare une
// classe ZoneGeographique locale dans ce meme namespace, qui masquerait
// l'entite de production. Voir GEO-9-Geofencing-Tests.cs.
//
// xUnit natif uniquement : aucune dependance a Moq ni FluentAssertions.
// ============================================================

using GeoTrack.Api.Controllers;
using GeoTrack.Api.Data;
using Prod = GeoTrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modeles = GeoTrack.Api.Models;

namespace GeoTrack.Api.Tests
{
    // =========================================================================
    // OUTILLAGE PARTAGE
    // =========================================================================

    /// <summary>Implementation muette d'INotificationService pour les tests.</summary>
    public sealed class StubNotificationsMuettes : Prod.INotificationService
    {
        public int NbAppels { get; private set; }

        public Task EnvoyerPush(string appareilId, string message, Prod.SeveriteAlerte severite)
        {
            NbAppels++;
            return Task.CompletedTask;
        }

        public Task EnvoyerSms(string appareilId, string message)
        {
            NbAppels++;
            return Task.CompletedTask;
        }

        public Task EnvoyerEmail(string appareilId, string message)
        {
            NbAppels++;
            return Task.CompletedTask;
        }

        public Task EnvoyerDashboard(string appareilId, Prod.ResultatEvaluation resultat)
        {
            NbAppels++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Implementation qui echoue systematiquement, pour eprouver la degradation
    /// gracieuse du chemin vitesse.
    /// </summary>
    public sealed class StubNotificationsDefaillantes : Prod.INotificationService
    {
        public Task EnvoyerPush(string appareilId, string message, Prod.SeveriteAlerte severite)
            => throw new InvalidOperationException("Panne simulee du canal push.");

        public Task EnvoyerSms(string appareilId, string message)
            => throw new InvalidOperationException("Panne simulee du canal SMS.");

        public Task EnvoyerEmail(string appareilId, string message)
            => throw new InvalidOperationException("Panne simulee du canal courriel.");

        public Task EnvoyerDashboard(string appareilId, Prod.ResultatEvaluation resultat)
            => throw new InvalidOperationException("Panne simulee du tableau de bord.");
    }

    /// <summary>Notificateur de zone qui capture les alertes, sans persistance.</summary>
    public sealed class StubNotificateurZone : Prod.INotificateurAlerteZone
    {
        public List<Prod.AlerteSortieZone> Alertes { get; } = new();

        public Task SignalerSortieDeZoneAsync(Prod.AlerteSortieZone alerte)
        {
            Alertes.Add(alerte);
            return Task.CompletedTask;
        }
    }

    public static class FabriqueControleurPositions
    {
        public static GeoTrackContext CreerContexteEnMemoire()
        {
            var options = new DbContextOptionsBuilder<GeoTrackContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new GeoTrackContext(options);
        }

        public static Prod.AlerteVitesseService ServiceVitesse(
            Prod.ConfigurationSeuil? config = null,
            Prod.INotificationService? notifications = null)
            => new(config ?? new Prod.ConfigurationSeuil(),
                   notifications ?? new StubNotificationsMuettes());

        public static PositionsGpsController Creer(
            GeoTrackContext context,
            Prod.INotificateurAlerteZone? notificateurZone = null,
            Prod.AlerteVitesseService? alerteVitesse = null)
            => new(context,
                   new Prod.GeofencingService(),
                   notificateurZone ?? new StubNotificateurZone(),
                   alerteVitesse ?? ServiceVitesse(),
                   NullLogger<PositionsGpsController>.Instance);
    }

    // =========================================================================
    // SOURCE 1 : ALERTES DE VITESSE
    // =========================================================================

    public class GEO58_AlerteVitesseTests
    {
        // Seuils explicites : le test ne doit pas dependre des valeurs par
        // defaut de ConfigurationSeuil, qui peuvent evoluer.
        private static Prod.ConfigurationSeuil Config() => new()
        {
            SeuilAvertissement = 55,
            SeuilAlerte = 60,
            SeuilCritique = 75,
            ToleranceGps = 3,
            CooldownMinutes = 5
        };

        private static Modeles.PositionGps PositionA(double vitesse, DateTime horodatage) => new()
        {
            VehiculeId = "VEH-001",
            Latitude = 45.4236,
            Longitude = -75.7009,
            Vitesse = vitesse,
            Horodatage = horodatage,
            EtatVehicule = "en_route"
        };

        private static readonly DateTime Depart = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task Vitesse_Critique_EnregistreUneAlerteEnBase()
        {
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            var controleur = FabriqueControleurPositions.Creer(
                context,
                alerteVitesse: FabriqueControleurPositions.ServiceVitesse(Config()));

            // 90 km/h - 3 de tolerance = 87, au-dela du seuil critique (75).
            // Le critique declenche immediatement, sans attendre d'echantillons.
            var resultat = await controleur.Recevoir(PositionA(90, Depart));

            Assert.IsType<OkObjectResult>(resultat);

            var alerte = Assert.Single(await context.Alertes.ToListAsync());
            Assert.Equal(Modeles.TypeAlerte.VitesseExcessive, alerte.TypeAlerte);
            Assert.Equal(Prod.SeveriteAlerte.Critique, alerte.Severite);
            Assert.Equal("VEH-001", alerte.VehiculeId);
            Assert.Equal(Depart, alerte.Date);
            Assert.Contains("90", alerte.Details);
        }

        [Fact]
        public async Task Vitesse_Normale_NEnregistreAucuneAlerte()
        {
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            var controleur = FabriqueControleurPositions.Creer(
                context,
                alerteVitesse: FabriqueControleurPositions.ServiceVitesse(Config()));

            await controleur.Recevoir(PositionA(40, Depart));

            Assert.Empty(await context.Alertes.ToListAsync());
            Assert.Single(await context.PositionsGps.ToListAsync());
        }

        [Fact]
        public async Task Vitesse_AntiSpam_NEnregistrePasDeDoublonPendantLeCooldown()
        {
            // Deux depassements critiques a une minute d'intervalle : le second
            // est bloque par le cooldown de 5 minutes de GEO-51. La table ne doit
            // donc contenir qu'une seule ligne, sans quoi on contournerait
            // l'anti-spam en le doublant cote persistance.
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            var controleur = FabriqueControleurPositions.Creer(
                context,
                alerteVitesse: FabriqueControleurPositions.ServiceVitesse(Config()));

            await controleur.Recevoir(PositionA(90, Depart));
            await controleur.Recevoir(PositionA(95, Depart.AddMinutes(1)));

            Assert.Single(await context.Alertes.ToListAsync());
            Assert.Equal(2, await context.PositionsGps.CountAsync());
        }

        [Fact]
        public async Task Vitesse_PanneDeNotification_NeBloquePasLIngestionGps()
        {
            // Degradation gracieuse : le canal de notification leve une exception,
            // la position doit malgre tout etre enregistree et la reponse rester
            // un 200. Perdre une alerte est preferable a perdre une position.
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            var controleur = FabriqueControleurPositions.Creer(
                context,
                alerteVitesse: FabriqueControleurPositions.ServiceVitesse(
                    Config(), new StubNotificationsDefaillantes()));

            var resultat = await controleur.Recevoir(PositionA(90, Depart));

            Assert.IsType<OkObjectResult>(resultat);
            Assert.Single(await context.PositionsGps.ToListAsync());
            Assert.Empty(await context.Alertes.ToListAsync());
        }
    }

    // =========================================================================
    // SOURCE 2 : ALERTES DE SORTIE DE ZONE
    // =========================================================================

    public class GEO58_AlerteGeofencingTests
    {
        private static readonly DateTime Depart = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

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

        private static Prod.NotificateurAlerteZonePersistant Notificateur(GeoTrackContext context)
            => new(context, NullLogger<Prod.NotificateurAlerteZonePersistant>.Instance);

        [Fact]
        public async Task SortieDeZone_EnregistreUneAlerteEnBase()
        {
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            await AjouterZoneAsync(context);

            var controleur = FabriqueControleurPositions.Creer(
                context, notificateurZone: Notificateur(context));

            await controleur.Recevoir(PositionA(45.4236, -75.7009, Depart));            // dedans
            await controleur.Recevoir(PositionA(45.5017, -73.5673, Depart.AddMinutes(5))); // dehors

            var alerte = Assert.Single(await context.Alertes.ToListAsync());
            Assert.Equal(Modeles.TypeAlerte.SortieZone, alerte.TypeAlerte);
            Assert.Equal(Prod.SeveriteAlerte.Alerte, alerte.Severite);
            Assert.Equal("VEH-001", alerte.VehiculeId);
            Assert.Equal(Depart.AddMinutes(5), alerte.Date);
            Assert.Contains("Depot central", alerte.Details);
        }

        [Fact]
        public async Task SortieDeZone_PasDeDoublonSiDejaDehors()
        {
            // La detection par transition de GEO-9 doit se refleter en base :
            // trois positions hors zone ne produisent qu'une seule alerte.
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            await AjouterZoneAsync(context);

            var controleur = FabriqueControleurPositions.Creer(
                context, notificateurZone: Notificateur(context));

            await controleur.Recevoir(PositionA(45.4236, -75.7009, Depart));
            await controleur.Recevoir(PositionA(45.5017, -73.5673, Depart.AddMinutes(5)));
            await controleur.Recevoir(PositionA(45.5020, -73.5680, Depart.AddMinutes(10)));
            await controleur.Recevoir(PositionA(45.5030, -73.5690, Depart.AddMinutes(15)));

            Assert.Single(await context.Alertes.ToListAsync());
        }

        [Fact]
        public async Task SortieDeZone_SansZone_NEnregistreRien()
        {
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();

            var controleur = FabriqueControleurPositions.Creer(
                context, notificateurZone: Notificateur(context));

            await controleur.Recevoir(PositionA(45.4236, -75.7009, Depart));
            await controleur.Recevoir(PositionA(45.5017, -73.5673, Depart.AddMinutes(5)));

            Assert.Empty(await context.Alertes.ToListAsync());
        }

        [Fact]
        public async Task SortieDeZone_PanneDePersistance_NePropageAucuneException()
        {
            // Contexte libere : toute ecriture echoue. Le notificateur doit
            // absorber la panne, puisqu'il est appele apres l'enregistrement de
            // la position et ne doit jamais faire echouer l'ingestion.
            var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            var notificateur = Notificateur(context);
            context.Dispose();

            var exception = await Record.ExceptionAsync(() =>
                notificateur.SignalerSortieDeZoneAsync(new Prod.AlerteSortieZone
                {
                    VehiculeId = "VEH-001",
                    ZoneId = 1,
                    NomZone = "Depot central",
                    Latitude = 45.5017,
                    Longitude = -73.5673,
                    DistanceMetres = 166_000,
                    RayonMetres = 500,
                    Horodatage = Depart
                }));

            Assert.Null(exception);
        }
    }

    // =========================================================================
    // ENDPOINT
    // =========================================================================

    public class GEO58_AlertesControllerTests
    {
        private static readonly DateTime Depart = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

        private static async Task SemerAsync(GeoTrackContext context)
        {
            context.Alertes.AddRange(
                new Modeles.Alerte
                {
                    Date = Depart.AddMinutes(-10),
                    VehiculeId = "VEH-001",
                    TypeAlerte = Modeles.TypeAlerte.VitesseExcessive,
                    Severite = Prod.SeveriteAlerte.Avertissement,
                    Details = "ancienne"
                },
                new Modeles.Alerte
                {
                    Date = Depart,
                    VehiculeId = "VEH-002",
                    TypeAlerte = Modeles.TypeAlerte.SortieZone,
                    Severite = Prod.SeveriteAlerte.Alerte,
                    Details = "recente"
                },
                new Modeles.Alerte
                {
                    Date = Depart.AddMinutes(-5),
                    VehiculeId = "VEH-001",
                    TypeAlerte = Modeles.TypeAlerte.VitesseExcessive,
                    Severite = Prod.SeveriteAlerte.Critique,
                    Details = "intermediaire"
                });

            await context.SaveChangesAsync();
        }

        private static List<Modeles.Alerte> Extraire(ActionResult<IEnumerable<Modeles.Alerte>> resultat)
        {
            var ok = Assert.IsType<OkObjectResult>(resultat.Result);
            return Assert.IsType<List<Modeles.Alerte>>(ok.Value);
        }

        [Fact]
        public void Controleur_EstProtegePar_Authorize()
        {
            var attributs = typeof(AlertesController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);

            Assert.NotEmpty(attributs);
        }

        [Fact]
        public async Task Get_TrieParDateDecroissante()
        {
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            await SemerAsync(context);

            var alertes = Extraire(await new AlertesController(context).Lister());

            Assert.Equal(3, alertes.Count);
            Assert.Equal(
                new[] { "recente", "intermediaire", "ancienne" },
                alertes.Select(a => a.Details).ToArray());
        }

        [Fact]
        public async Task Get_FiltreParVehicule()
        {
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            await SemerAsync(context);

            var alertes = Extraire(await new AlertesController(context).Lister("VEH-001"));

            Assert.Equal(2, alertes.Count);
            Assert.All(alertes, a => Assert.Equal("VEH-001", a.VehiculeId));
            // Le tri doit survivre au filtrage.
            Assert.Equal("intermediaire", alertes[0].Details);
        }

        [Fact]
        public async Task Get_FiltreInconnu_RenvoieUneListeVide()
        {
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            await SemerAsync(context);

            var alertes = Extraire(await new AlertesController(context).Lister("VEH-INEXISTANT"));

            Assert.Empty(alertes);
        }

        [Fact]
        public async Task Get_SansAlerte_RenvoieUneListeVide()
        {
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();

            var alertes = Extraire(await new AlertesController(context).Lister());

            Assert.Empty(alertes);
        }

        [Fact]
        public async Task Get_LesDeuxSourcesCohabitent()
        {
            using var context = FabriqueControleurPositions.CreerContexteEnMemoire();
            await SemerAsync(context);

            var alertes = Extraire(await new AlertesController(context).Lister());

            Assert.Contains(alertes, a => a.TypeAlerte == Modeles.TypeAlerte.VitesseExcessive);
            Assert.Contains(alertes, a => a.TypeAlerte == Modeles.TypeAlerte.SortieZone);
        }
    }
}
