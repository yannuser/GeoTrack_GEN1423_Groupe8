// ============================================================
// GEO-15 : tests unitaires du VRAI VehiculeService
//
// A ne pas confondre avec GEO-15-VehiculeCrud-Tests.cs, qui teste une classe
// `VehiculeServiceTest` redefinie localement dans le fichier de tests et sans
// aucun lien avec le service de production. Ce fichier-ci exerce le vrai
// GeoTrack.Api.Services.VehiculeService, branche sur les vrais depots EF Core
// (VehiculeRepository / ConducteurRepository / GroupeRepository) au-dessus
// d'une base InMemory, selon la convention deja utilisee par
// PositionsGpsControllerTests.
//
// POURQUOI L'ALIAS `Geo15` : GEO-15-VehiculeCrud-Tests.cs declare, dans ce meme
// namespace GeoTrack.Api.Tests, sept types homonymes de ceux de production
// (Vehicule, CreerVehiculeRequest, PositionGpsEvent, ResultatOperation<T>,
// TypeVehicule, StatutVehicule, StatutGPS). Les types d'un namespace englobant
// l'emportent sur ceux importes par un using : ecrits sans prefixe, les noms
// ci-dessous designeraient les copies du fichier de tests, dont les definitions
// ont d'ailleurs diverge (StatutGPS.NonConfigure cote tests contre
// StatutGPS.NonConnecte cote production). Un alias de namespace, lui, ne peut
// pas etre masque : il garantit qu'on teste bien le code de production, sans
// avoir a toucher au fichier de tests existant.
//
// xUnit natif uniquement : aucune dependance a Moq ni FluentAssertions.
// ============================================================

using GeoTrack.Api.Data;
using GeoTrack.Api.Data.Repositories;
using GeoTrack.Api.Models;
using Microsoft.EntityFrameworkCore;
using Geo15 = GeoTrack.Api.Services;

namespace GeoTrack.Api.Tests
{
    public class GEO15_VehiculeServiceTests
    {
        // Chaque test recoit sa propre base en memoire, isolee des autres.
        private static GeoTrackContext CreerContexteEnMemoire()
        {
            var options = new DbContextOptionsBuilder<GeoTrackContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new GeoTrackContext(options);
        }

        private static Geo15.VehiculeService CreerService(GeoTrackContext context)
            => new(
                new VehiculeRepository(context),
                new ConducteurRepository(context),
                new GroupeRepository(context));

        private static Geo15.CreerVehiculeRequest RequeteValide() => new()
        {
            Immatriculation = "ABC123",
            Marque = "Ford",
            Modele = "Transit",
            Annee = 2022,
            Type = Geo15.TypeVehicule.Fourgonnette,
            TrackerGpsId = "TRK-001"
        };

        // ============================================================
        // CREATION
        // ============================================================

        [Fact]
        public async Task Creer_EnregistreLeVehicule_EtLeMetEnAttente()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var resultat = await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            Assert.True(resultat.Succes);
            Assert.NotNull(resultat.Donnees);
            Assert.True(resultat.Donnees.Id > 0);
            Assert.Equal(Geo15.StatutVehicule.EnAttente, resultat.Donnees.Statut);
            Assert.Equal(Geo15.StatutGPS.NonConnecte, resultat.Donnees.StatutGps);
            Assert.Equal("testeur", resultat.Donnees.CreePar);

            // Reellement persiste, pas seulement retourne.
            Assert.Equal(1, await context.Vehicules.CountAsync());
        }

        [Fact]
        public async Task Creer_NormaliseImmatriculationEnMajuscules()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var requete = RequeteValide();
            requete.Immatriculation = "  abc123  ";

            var resultat = await service.CreerVehiculeAsync(requete, "testeur");

            Assert.True(resultat.Succes);
            Assert.Equal("ABC123", resultat.Donnees.Immatriculation);
        }

        [Fact]
        public async Task Creer_RefuseUneImmatriculationEnDoublon()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var doublon = RequeteValide();
            doublon.TrackerGpsId = "TRK-002"; // seul le tracker differe

            var resultat = await service.CreerVehiculeAsync(doublon, "testeur");

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("ABC123"));
            Assert.Equal(1, await context.Vehicules.CountAsync());
        }

        [Fact]
        public async Task Creer_RefuseUnTrackerGpsDejaAssigne()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var doublon = RequeteValide();
            doublon.Immatriculation = "XYZ789"; // seule l'immatriculation differe

            var resultat = await service.CreerVehiculeAsync(doublon, "testeur");

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("TRK-001"));
        }

        [Fact]
        public async Task Creer_RefuseUnVinEnDoublon()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var premier = RequeteValide();
            premier.VIN = "1HGBH41JXMN109186";
            await service.CreerVehiculeAsync(premier, "testeur");

            var second = RequeteValide();
            second.Immatriculation = "XYZ789";
            second.TrackerGpsId = "TRK-002";
            second.VIN = "1HGBH41JXMN109186";

            var resultat = await service.CreerVehiculeAsync(second, "testeur");

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("VIN"));
        }

        [Fact]
        public async Task Creer_AcceptePlusieursVehiculesSansVin()
        {
            // Le VIN est optionnel : deux vehicules sans VIN ne doivent pas
            // etre pris pour des doublons.
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var second = RequeteValide();
            second.Immatriculation = "XYZ789";
            second.TrackerGpsId = "TRK-002";

            var resultat = await service.CreerVehiculeAsync(second, "testeur");

            Assert.True(resultat.Succes);
            Assert.Equal(2, await context.Vehicules.CountAsync());
        }

        [Theory]
        [InlineData(9)]
        [InlineData(201)]
        public async Task Creer_RefuseUneVitesseMaxHorsBornes(double vitesse)
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var requete = RequeteValide();
            requete.VitesseMaxKmh = vitesse;

            var resultat = await service.CreerVehiculeAsync(requete, "testeur");

            Assert.False(resultat.Succes);
            Assert.Contains("10", resultat.Message);
            Assert.Equal(0, await context.Vehicules.CountAsync());
        }

        [Theory]
        [InlineData(10)]
        [InlineData(200)]
        public async Task Creer_AccepteUneVitesseMaxAuxBornes(double vitesse)
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var requete = RequeteValide();
            requete.VitesseMaxKmh = vitesse;

            var resultat = await service.CreerVehiculeAsync(requete, "testeur");

            Assert.True(resultat.Succes);
            Assert.Equal(vitesse, resultat.Donnees.VitesseMaxKmh);
        }

        // ============================================================
        // RECEPTION POSITION GPS (critere d'acceptation #2)
        // ============================================================

        [Fact]
        public async Task PositionGps_PremierePosition_RendLeVehiculeActif()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var resultat = await service.TraiterPositionGpsAsync(new Geo15.PositionGpsEvent
            {
                TrackerGpsId = "TRK-001",
                Latitude = 45.4215,
                Longitude = -75.6972,
                VitesseKmh = 48
            });

            Assert.True(resultat.Succes);
            Assert.Equal(Geo15.StatutVehicule.Actif, resultat.Donnees.Statut);
            Assert.Equal(45.4215, resultat.Donnees.Latitude);
            Assert.Equal(-75.6972, resultat.Donnees.Longitude);
            Assert.Contains("carte", resultat.Message);

            var enBase = await context.Vehicules.SingleAsync();
            Assert.Equal(Geo15.StatutGPS.PremierePositionRecue, enBase.StatutGps);
            Assert.Equal(45.4215, enBase.PremiereLat);
            Assert.NotNull(enBase.PremierePositionDate);
        }

        [Fact]
        public async Task PositionGps_TrackerInconnu_RetourneUnEchec()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var resultat = await service.TraiterPositionGpsAsync(new Geo15.PositionGpsEvent
            {
                TrackerGpsId = "TRK-INEXISTANT",
                Latitude = 45.0,
                Longitude = -75.0
            });

            Assert.False(resultat.Succes);
            Assert.Contains("TRK-INEXISTANT", resultat.Message);
        }

        [Fact]
        public async Task PositionGps_PositionsSuivantes_NeMettentPasAJourLaPositionStockee()
        {
            // Documente une limitation CONNUE de la conception GEO-15 :
            // seules PremiereLat/PremiereLng sont persistees. Les positions
            // suivantes ne changent que StatutGps, si bien que la position
            // stockee sur le vehicule reste figee sur la toute premiere.
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            await service.TraiterPositionGpsAsync(new Geo15.PositionGpsEvent
            {
                TrackerGpsId = "TRK-001",
                Latitude = 45.4215,
                Longitude = -75.6972
            });

            var resultat = await service.TraiterPositionGpsAsync(new Geo15.PositionGpsEvent
            {
                TrackerGpsId = "TRK-001",
                Latitude = 46.8139,
                Longitude = -71.2080
            });

            Assert.True(resultat.Succes);
            // Le DTO renvoie bien la nouvelle position...
            Assert.Equal(46.8139, resultat.Donnees.Latitude);

            // ...mais la base garde la premiere.
            var enBase = await context.Vehicules.SingleAsync();
            Assert.Equal(Geo15.StatutGPS.Connecte, enBase.StatutGps);
            Assert.Equal(45.4215, enBase.PremiereLat);
        }

        // ============================================================
        // MODIFICATION
        // ============================================================

        [Fact]
        public async Task Modifier_AppliqueLesChampsFournis()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var cree = await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var resultat = await service.ModifierVehiculeAsync(cree.Donnees.Id, new Geo15.ModifierVehiculeRequest
            {
                Marque = "Mercedes",
                Modele = "Sprinter",
                Statut = Geo15.StatutVehicule.EnMaintenance
            });

            Assert.True(resultat.Succes);
            Assert.Equal("Mercedes", resultat.Donnees.Marque);
            Assert.Equal("Sprinter", resultat.Donnees.Modele);
            Assert.Equal(Geo15.StatutVehicule.EnMaintenance, resultat.Donnees.Statut);
            Assert.NotNull(resultat.Donnees.DateModification);

            // Les champs non fournis restent inchanges.
            Assert.Equal(2022, resultat.Donnees.Annee);
        }

        [Fact]
        public async Task Modifier_VehiculeInexistant_RetourneUnEchec()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var resultat = await service.ModifierVehiculeAsync(999, new Geo15.ModifierVehiculeRequest
            {
                Marque = "Ford"
            });

            Assert.False(resultat.Succes);
            Assert.Contains("introuvable", resultat.Message);
        }

        [Fact]
        public async Task Modifier_RefuseUnTrackerDejaAssigneAUnAutreVehicule()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var second = RequeteValide();
            second.Immatriculation = "XYZ789";
            second.TrackerGpsId = "TRK-002";
            var cree = await service.CreerVehiculeAsync(second, "testeur");

            var resultat = await service.ModifierVehiculeAsync(cree.Donnees.Id, new Geo15.ModifierVehiculeRequest
            {
                TrackerGpsId = "TRK-001"
            });

            Assert.False(resultat.Succes);
            Assert.Contains("TRK-001", resultat.Message);
        }

        [Fact]
        public async Task Modifier_AutoriseAConserverSonPropreTracker()
        {
            // Verifie que le parametre excludeId des tests d'unicite fonctionne :
            // reenvoyer son propre tracker ne doit pas etre vu comme un conflit.
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var cree = await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var resultat = await service.ModifierVehiculeAsync(cree.Donnees.Id, new Geo15.ModifierVehiculeRequest
            {
                TrackerGpsId = "TRK-001",
                Marque = "Ford Pro"
            });

            Assert.True(resultat.Succes);
            Assert.Equal("Ford Pro", resultat.Donnees.Marque);
        }

        // ============================================================
        // SUPPRESSION
        // ============================================================

        [Fact]
        public async Task Supprimer_RetireUnVehiculeNonActif()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var cree = await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var resultat = await service.SupprimerVehiculeAsync(cree.Donnees.Id);

            Assert.True(resultat.Succes);
            Assert.Equal(0, await context.Vehicules.CountAsync());
        }

        [Fact]
        public async Task Supprimer_RefuseUnVehiculeActif()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var cree = await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            // Une premiere position GPS rend le vehicule actif.
            await service.TraiterPositionGpsAsync(new Geo15.PositionGpsEvent
            {
                TrackerGpsId = "TRK-001",
                Latitude = 45.0,
                Longitude = -75.0
            });

            var resultat = await service.SupprimerVehiculeAsync(cree.Donnees.Id);

            Assert.False(resultat.Succes);
            Assert.Contains("Inactif", resultat.Message);
            Assert.Equal(1, await context.Vehicules.CountAsync());
        }

        [Fact]
        public async Task Supprimer_VehiculeInexistant_RetourneUnEchec()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var resultat = await service.SupprimerVehiculeAsync(999);

            Assert.False(resultat.Succes);
            Assert.Contains("introuvable", resultat.Message);
        }

        // ============================================================
        // LISTES ET FILTRES
        // ============================================================

        [Fact]
        public async Task Lister_RenvoieTousLesVehicules()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var second = RequeteValide();
            second.Immatriculation = "XYZ789";
            second.TrackerGpsId = "TRK-002";
            await service.CreerVehiculeAsync(second, "testeur");

            var resultat = await service.ListerVehiculesAsync();

            Assert.True(resultat.Succes);
            Assert.Equal(2, resultat.Donnees.Count);
            Assert.Contains("2 v", resultat.Message);
        }

        [Fact]
        public async Task Lister_FiltreParStatut()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var second = RequeteValide();
            second.Immatriculation = "XYZ789";
            second.TrackerGpsId = "TRK-002";
            await service.CreerVehiculeAsync(second, "testeur");

            // Seul le premier recoit une position et devient Actif.
            await service.TraiterPositionGpsAsync(new Geo15.PositionGpsEvent
            {
                TrackerGpsId = "TRK-001",
                Latitude = 45.0,
                Longitude = -75.0
            });

            var actifs = await service.ListerVehiculesAsync(Geo15.StatutVehicule.Actif);
            var enAttente = await service.ListerVehiculesAsync(Geo15.StatutVehicule.EnAttente);

            Assert.Single(actifs.Donnees);
            Assert.Equal("ABC123", actifs.Donnees[0].Immatriculation);
            Assert.Single(enAttente.Donnees);
            Assert.Equal("XYZ789", enAttente.Donnees[0].Immatriculation);
        }

        // ============================================================
        // VERIFICATIONS D'UNICITE (validation formulaire temps reel)
        // ============================================================

        [Fact]
        public async Task VerifierUniciteImmatriculation_FauxSiDejaPrise()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            Assert.False(await service.VerifierUniciteImmatriculationAsync("ABC123"));
            Assert.True(await service.VerifierUniciteImmatriculationAsync("NOUVELLE"));
        }

        [Fact]
        public async Task VerifierUniciteTracker_IgnoreLeVehiculeExclu()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var cree = await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            Assert.False(await service.VerifierUniciteTrackerAsync("TRK-001"));
            Assert.True(await service.VerifierUniciteTrackerAsync("TRK-001", cree.Donnees.Id));
        }

        // ============================================================
        // DONNEES DE FORMULAIRE : CONDUCTEURS ET GROUPES
        // ============================================================

        [Fact]
        public async Task Conducteurs_NeRenvoieQueCeuxNonAffectes()
        {
            using var context = CreerContexteEnMemoire();

            context.Conducteurs.AddRange(
                new Conducteur { Nom = "Jean Tremblay" },
                new Conducteur { Nom = "Marie Dubois" });
            await context.SaveChangesAsync();

            var affecte = await context.Conducteurs.SingleAsync(c => c.Nom == "Jean Tremblay");

            var service = CreerService(context);

            var requete = RequeteValide();
            requete.ConducteurId = affecte.Id;
            await service.CreerVehiculeAsync(requete, "testeur");

            var disponibles = await service.GetConducteursDisponiblesAsync();

            Assert.Single(disponibles);
            Assert.Equal("Marie Dubois", disponibles[0].Nom);
        }

        [Fact]
        public async Task Conducteurs_TousDisponiblesQuandAucuneAffectation()
        {
            using var context = CreerContexteEnMemoire();

            context.Conducteurs.AddRange(
                new Conducteur { Nom = "Jean Tremblay" },
                new Conducteur { Nom = "Marie Dubois" });
            await context.SaveChangesAsync();

            var service = CreerService(context);

            var disponibles = await service.GetConducteursDisponiblesAsync();

            Assert.Equal(2, disponibles.Count);
            // Tri alphabetique.
            Assert.Equal("Jean Tremblay", disponibles[0].Nom);
        }

        [Fact]
        public async Task Groupes_RenvoieLesValeursDistinctesTriees()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            var a = RequeteValide();
            a.GroupeDivision = "Livraison";
            await service.CreerVehiculeAsync(a, "testeur");

            var b = RequeteValide();
            b.Immatriculation = "XYZ789";
            b.TrackerGpsId = "TRK-002";
            b.GroupeDivision = "Livraison"; // doublon volontaire
            await service.CreerVehiculeAsync(b, "testeur");

            var c = RequeteValide();
            c.Immatriculation = "DEF456";
            c.TrackerGpsId = "TRK-003";
            c.GroupeDivision = "Atelier";
            await service.CreerVehiculeAsync(c, "testeur");

            var groupes = await service.GetGroupesAsync();

            Assert.Equal(new[] { "Atelier", "Livraison" }, groupes);
        }

        [Fact]
        public async Task Groupes_IgnoreLesVehiculesSansGroupe()
        {
            using var context = CreerContexteEnMemoire();
            var service = CreerService(context);

            await service.CreerVehiculeAsync(RequeteValide(), "testeur");

            var groupes = await service.GetGroupesAsync();

            Assert.Empty(groupes);
        }
    }
}
