// ============================================================
// GEO-58 : validation du graphe d'injection de dependances
//
// RAISON D'ETRE : la PR #18 avait fusionne un PositionsGpsController exigeant
// GeofencingService et INotificateurAlerteZone sans que Program.cs ne les
// enregistre. La compilation restait verte — le .NET ne verifie pas le graphe
// DI a la compilation — et /api/positionsgps renvoyait 500 en production.
//
// Ces tests construisent chaque controleur a partir du VRAI conteneur bati par
// Program.cs, via ActivatorUtilities.CreateInstance : toute dependance non
// enregistree fait echouer le test au lieu d'attendre la mise en production.
// Tout nouveau controleur devrait etre ajoute a la theorie ci-dessous.
//
// FabriqueApiTest (definie dans GEO-18-Authentification-Tests.cs) substitue
// EF InMemory a SqlServer : aucune base reelle n'est requise.
// ============================================================

using GeoTrack.Api.Controllers;
using Prod = GeoTrack.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GeoTrack.Api.Tests
{
    public class GEO58_InjectionDependancesTests : IClassFixture<FabriqueApiTest>
    {
        private readonly FabriqueApiTest _fabrique;

        public GEO58_InjectionDependancesTests(FabriqueApiTest fabrique)
        {
            _fabrique = fabrique;
        }

        [Theory]
        [InlineData(typeof(PositionsGpsController))]
        [InlineData(typeof(ZonesController))]
        [InlineData(typeof(AlertesController))]
        [InlineData(typeof(TrajetsController))]
        [InlineData(typeof(VehiculesController))]
        [InlineData(typeof(HealthController))]
        [InlineData(typeof(AuthController))]
        public void ChaqueControleur_SeConstruitDepuisLeConteneurReel(Type typeControleur)
        {
            using var portee = _fabrique.Services.CreateScope();

            // Leve si une seule dependance du constructeur n'est pas enregistree.
            var controleur = ActivatorUtilities.CreateInstance(
                portee.ServiceProvider, typeControleur);

            Assert.NotNull(controleur);
        }

        [Theory]
        [InlineData(typeof(Prod.GeofencingService))]
        [InlineData(typeof(Prod.INotificateurAlerteZone))]
        [InlineData(typeof(Prod.AlerteVitesseService))]
        [InlineData(typeof(Prod.INotificationService))]
        [InlineData(typeof(Prod.ConfigurationSeuil))]
        public void ChaqueServiceMetier_EstEnregistre(Type typeService)
        {
            using var portee = _fabrique.Services.CreateScope();

            Assert.NotNull(portee.ServiceProvider.GetService(typeService));
        }

        [Fact]
        public void AlerteVitesseService_EstUnSingleton()
        {
            // Propriete de correction, pas de style : le service porte en memoire
            // la machine a etats et les compteurs anti-spam de chaque appareil.
            // Deux instances distinctes signifieraient qu'un depassement de
            // vitesse ne serait jamais confirme d'une requete a l'autre.
            using var portee1 = _fabrique.Services.CreateScope();
            using var portee2 = _fabrique.Services.CreateScope();

            var premier = portee1.ServiceProvider.GetRequiredService<Prod.AlerteVitesseService>();
            var second = portee2.ServiceProvider.GetRequiredService<Prod.AlerteVitesseService>();

            Assert.Same(premier, second);
        }

        [Fact]
        public void AlerteVitesseService_SeResoutDepuisLaRacine_DoncSansDependanceCaptive()
        {
            // Un Singleton qui dependrait d'un service Scoped serait une
            // dependance captive : elle survivrait a sa portee. Obtenir le
            // service directement depuis le fournisseur racine, hors de toute
            // portee, prouve que toute sa chaine de dependances est Singleton.
            var service = _fabrique.Services.GetRequiredService<Prod.AlerteVitesseService>();

            Assert.NotNull(service);
        }

        [Fact]
        public void NotificateurAlerteZone_EstLImplementationPersistante()
        {
            // GEO-58 a substitue NotificateurAlerteZonePersistant a la version
            // purement journalisante. Sans cette verification, un retour en
            // arriere sur cette ligne de Program.cs cesserait silencieusement
            // d'alimenter la table Alertes.
            using var portee = _fabrique.Services.CreateScope();

            var notificateur = portee.ServiceProvider
                .GetRequiredService<Prod.INotificateurAlerteZone>();

            Assert.IsType<Prod.NotificateurAlerteZonePersistant>(notificateur);
        }
    }
}
