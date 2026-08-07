using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using GeoTrack.Api.Services;
using Xunit;

// ATTENTION AU NAMESPACE
// ---------------------------------------------------------------------------
// Ce fichier vit volontairement en dehors de `GeoTrack.Api.Tests`.
//
// GEO-52-AlerteVitesse-Tests.cs redefinit dans ce namespace ses propres copies
// de AlerteVitesseService, ConfigurationSeuil, SeveriteAlerte, etc. En C#, les
// types d'un namespace englobant l'emportent sur les directives `using` : un
// test place dans `GeoTrack.Api.Tests` qui ecrirait `using GeoTrack.Api.Services;`
// testerait silencieusement la copie de GEO-52, pas le vrai service.
//
// Un namespace non imbrique elimine l'ambiguite : ici, AlerteVitesseService
// designe sans equivoque GeoTrack.Api.Services.AlerteVitesseService.
// ---------------------------------------------------------------------------
namespace GeoTrack.Tests.Geo51
{
    /// <summary>Enregistre les notifications emises. Sur de l'acces concurrent.</summary>
    public sealed class StubNotifications : INotificationService
    {
        private readonly ConcurrentQueue<(string AppareilId, string Message, SeveriteAlerte Severite)> _push = new();
        private readonly ConcurrentQueue<string> _sms = new();
        private readonly ConcurrentQueue<string> _email = new();
        private readonly ConcurrentQueue<ResultatEvaluation> _dashboard = new();

        public int NbPush => _push.Count;
        public int NbSms => _sms.Count;
        public int NbEmail => _email.Count;
        public int NbDashboard => _dashboard.Count;

        public int NbPushPour(SeveriteAlerte severite) => _push.Count(p => p.Severite == severite);

        public Task EnvoyerPush(string appareilId, string message, SeveriteAlerte severite)
        {
            _push.Enqueue((appareilId, message, severite));
            return Task.CompletedTask;
        }

        public Task EnvoyerSms(string appareilId, string message)
        {
            _sms.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task EnvoyerEmail(string appareilId, string message)
        {
            _email.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task EnvoyerDashboard(string appareilId, ResultatEvaluation resultat)
        {
            _dashboard.Enqueue(resultat);
            return Task.CompletedTask;
        }
    }

    public abstract class BaseTestsAlerteVitesse
    {
        /// <summary>
        /// Horodatage de reference volontairement fixe et dans le passe :
        /// si le service lisait encore l'heure systeme, les tests temporels
        /// ci-dessous echoueraient.
        /// </summary>
        protected static readonly DateTime T0 = new(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc);

        protected static DonneeVitesse Mesure(
            double vitesse, DateTime horodatage, string appareilId = "VH-001") => new()
        {
            AppareilId = appareilId,
            Vitesse = vitesse,
            Latitude = 45.4765,
            Longitude = -75.7013,
            Horodatage = horodatage
        };
    }

    // =======================================================================
    // 1. Horloge unifiee : tout se decide sur donnee.Horodatage
    // =======================================================================
    public class HorlogeUnifieeTests : BaseTestsAlerteVitesse
    {
        private readonly StubNotifications _notif = new();
        private readonly AlerteVitesseService _service;

        public HorlogeUnifieeTests()
        {
            _service = new AlerteVitesseService(new ConfigurationSeuil(), _notif);
        }

        [Fact]
        public async Task Cooldown_SeMesureSurLHorodatage_PasSurLHeureSysteme()
        {
            // Deux mesures critiques espacees de 10 minutes DANS LE TEMPS DE MESURE,
            // mais emises a quelques microsecondes d'intervalle en temps reel.
            // Cooldown = 5 min : la seconde doit passer.
            var premiere = await _service.EvaluerVitesse(Mesure(90, T0));
            var seconde = await _service.EvaluerVitesse(Mesure(90, T0.AddMinutes(10)));

            Assert.True(premiere.AlerteEnvoyee);
            Assert.True(
                seconde.AlerteEnvoyee,
                "10 minutes separent les deux mesures : le cooldown de 5 min est ecoule.");
            Assert.Equal(2, _notif.NbPushPour(SeveriteAlerte.Critique));
        }

        [Fact]
        public async Task Cooldown_BloqueDeuxMesuresTropRapprochees()
        {
            await _service.EvaluerVitesse(Mesure(90, T0));
            var seconde = await _service.EvaluerVitesse(Mesure(90, T0.AddMinutes(1)));

            Assert.False(seconde.AlerteEnvoyee);
            Assert.Equal("Alerte bloquée par anti-spam", seconde.Raison);
            Assert.Equal(1, _notif.NbPushPour(SeveriteAlerte.Critique));
        }

        [Fact]
        public async Task FenetreHoraire_SeReinitialiseSurLHorodatage()
        {
            var config = new ConfigurationSeuil { MaxAlertesParHeure = 1, CooldownMinutes = 0 };
            var notif = new StubNotifications();
            var service = new AlerteVitesseService(config, notif);

            var premiere = await service.EvaluerVitesse(Mesure(90, T0));
            var bloquee = await service.EvaluerVitesse(Mesure(90, T0.AddMinutes(2)));
            // Plus d'une heure de temps-mesure ecoulee : le quota horaire repart a zero.
            var apresFenetre = await service.EvaluerVitesse(Mesure(90, T0.AddMinutes(61)));

            Assert.True(premiere.AlerteEnvoyee);
            Assert.False(bloquee.AlerteEnvoyee);
            Assert.True(apresFenetre.AlerteEnvoyee);
        }

        [Fact]
        public async Task FenetreJournaliere_SeReinitialiseAuChangementDeJourMesure()
        {
            var config = new ConfigurationSeuil { MaxAlertesParJour = 1, CooldownMinutes = 0 };
            var notif = new StubNotifications();
            var service = new AlerteVitesseService(config, notif);

            var jour1 = await service.EvaluerVitesse(Mesure(90, T0));
            var jour1Bis = await service.EvaluerVitesse(Mesure(90, T0.AddMinutes(5)));
            var jour2 = await service.EvaluerVitesse(Mesure(90, T0.AddDays(1)));

            Assert.True(jour1.AlerteEnvoyee);
            Assert.False(jour1Bis.AlerteEnvoyee);
            Assert.True(jour2.AlerteEnvoyee, "Nouveau jour de mesure : le quota journalier repart a zero.");
        }

        [Fact]
        public async Task Evaluation_EstDeterministe_MemeAvecDesHorodatagesFuturs()
        {
            // Un horodatage dans le futur ne doit rien changer : aucune comparaison
            // n'est faite avec l'heure systeme.
            var futur = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var premiere = await _service.EvaluerVitesse(Mesure(90, futur));
            var seconde = await _service.EvaluerVitesse(Mesure(90, futur.AddMinutes(10)));

            Assert.True(premiere.AlerteEnvoyee);
            Assert.True(seconde.AlerteEnvoyee);
        }

        [Fact]
        public async Task Resultat_ReprendLHorodatageDeLaMesure()
        {
            var resultat = await _service.EvaluerVitesse(Mesure(90, T0));

            Assert.Equal(T0, resultat.Horodatage);
        }
    }

    // =======================================================================
    // 2. Purge des contextes inactifs
    // =======================================================================
    public class PurgeContextesTests : BaseTestsAlerteVitesse
    {
        private static AlerteVitesseService Service(int retentionHeures, int intervallePurgeMinutes)
            => new(
                new ConfigurationSeuil
                {
                    RetentionContexteHeures = retentionHeures,
                    IntervallePurgeMinutes = intervallePurgeMinutes
                },
                new StubNotifications());

        [Fact]
        public async Task Depart_AucunContexte()
        {
            Assert.Equal(0, Service(24, 30).NombreContextesSuivis);
        }

        [Fact]
        public async Task ChaqueAppareil_ObtientSonContexte()
        {
            var service = Service(24, 30);

            await service.EvaluerVitesse(Mesure(40, T0, "VH-001"));
            await service.EvaluerVitesse(Mesure(40, T0, "VH-002"));
            await service.EvaluerVitesse(Mesure(40, T0, "VH-003"));

            Assert.Equal(3, service.NombreContextesSuivis);
        }

        [Fact]
        public async Task Purge_OublieLesAppareilsSilencieuxTropLongtemps()
        {
            var service = Service(retentionHeures: 1, intervallePurgeMinutes: 0);

            await service.EvaluerVitesse(Mesure(40, T0, "VH-001"));
            Assert.Equal(1, service.NombreContextesSuivis);

            // VH-001 n'a plus rien emis depuis 2 h de temps-mesure : il sort.
            await service.EvaluerVitesse(Mesure(40, T0.AddHours(2), "VH-002"));

            Assert.Equal(1, service.NombreContextesSuivis);
        }

        [Fact]
        public async Task Purge_ConserveLesAppareilsEncoreActifs()
        {
            var service = Service(retentionHeures: 1, intervallePurgeMinutes: 0);

            await service.EvaluerVitesse(Mesure(40, T0, "VH-001"));
            await service.EvaluerVitesse(Mesure(40, T0.AddMinutes(30), "VH-002"));

            Assert.Equal(2, service.NombreContextesSuivis);
        }

        [Fact]
        public async Task Purge_NeSupprimeJamaisLeContexteEnCoursDeTraitement()
        {
            // Retention nulle et purge a chaque appel : le contexte courant doit
            // malgre tout survivre, sinon l'evaluation en cours perdrait son etat.
            var service = Service(retentionHeures: 0, intervallePurgeMinutes: 0);

            await service.EvaluerVitesse(Mesure(40, T0, "VH-001"));
            await service.EvaluerVitesse(Mesure(40, T0.AddHours(10), "VH-001"));

            Assert.Equal(1, service.NombreContextesSuivis);
        }

        [Fact]
        public async Task Purge_EstEspaceeParIntervallePurgeMinutes()
        {
            var service = Service(retentionHeures: 0, intervallePurgeMinutes: 30);

            // Premier balayage : ne retire rien (VH-001 est le contexte courant).
            await service.EvaluerVitesse(Mesure(40, T0, "VH-001"));

            // 10 min plus tard : balayage saute malgre une retention nulle.
            await service.EvaluerVitesse(Mesure(40, T0.AddMinutes(10), "VH-002"));
            Assert.Equal(2, service.NombreContextesSuivis);

            // 40 min apres le dernier balayage : il se declenche et purge les deux
            // premiers, seul VH-003 (courant) subsiste.
            await service.EvaluerVitesse(Mesure(40, T0.AddMinutes(40), "VH-003"));
            Assert.Equal(1, service.NombreContextesSuivis);
        }
    }

    // =======================================================================
    // 3. Acces concurrent au dictionnaire de contextes
    // =======================================================================
    public class ConcurrenceTests : BaseTestsAlerteVitesse
    {
        [Fact]
        public async Task CentAppareilsEnParallele_ChacunSonContexte()
        {
            var service = new AlerteVitesseService(new ConfigurationSeuil(), new StubNotifications());

            var taches = Enumerable.Range(0, 200)
                .Select(i => service.EvaluerVitesse(Mesure(40, T0, $"VH-{i:D3}")));

            await Task.WhenAll(taches);

            Assert.Equal(200, service.NombreContextesSuivis);
        }

        [Fact]
        public async Task MemeAppareilEnParallele_UnSeulContexte()
        {
            var service = new AlerteVitesseService(new ConfigurationSeuil(), new StubNotifications());

            var taches = Enumerable.Range(0, 200)
                .Select(_ => service.EvaluerVitesse(Mesure(40, T0, "VH-001")));

            await Task.WhenAll(taches);

            Assert.Equal(1, service.NombreContextesSuivis);
        }
    }

    // =======================================================================
    // 4. Machine a etats — garde-fou de non-regression
    //    Normal -> EnObservation -> Declenchee -> Escaladee
    // =======================================================================
    public class MachineAEtatsTests : BaseTestsAlerteVitesse
    {
        private readonly StubNotifications _notif = new();
        private readonly AlerteVitesseService _service;

        // Valeurs par defaut : tolerance 3, avertissement 55, alerte 60, critique 75,
        // 3 echantillons, 5 s de duree minimum.
        public MachineAEtatsTests()
        {
            _service = new AlerteVitesseService(new ConfigurationSeuil(), _notif);
        }

        [Fact]
        public async Task VitesseNormale_ResteEnEtatNormal()
        {
            var resultat = await _service.EvaluerVitesse(Mesure(45, T0));

            Assert.Equal(SeveriteAlerte.Aucune, resultat.Severite);
            Assert.Equal(EtatSurveillance.Normal, resultat.Etat);
            Assert.False(resultat.AlerteEnvoyee);
            Assert.Equal(0, _notif.NbPush);
        }

        [Fact]
        public async Task ToleranceGps_EstRetrancheeAvantComparaison()
        {
            // 57 - 3 = 54, juste sous le seuil d'avertissement (55).
            var resultat = await _service.EvaluerVitesse(Mesure(57, T0));

            Assert.Equal(SeveriteAlerte.Aucune, resultat.Severite);
        }

        [Fact]
        public async Task PremierDepassement_PasseEnObservation()
        {
            // 60 - 3 = 57 : avertissement.
            var resultat = await _service.EvaluerVitesse(Mesure(60, T0));

            Assert.Equal(SeveriteAlerte.Avertissement, resultat.Severite);
            Assert.Equal(EtatSurveillance.EnObservation, resultat.Etat);
            Assert.False(resultat.AlerteEnvoyee);
        }

        [Fact]
        public async Task TroisEchantillons_EtDureeSuffisante_PassentEnDeclenchee()
        {
            await _service.EvaluerVitesse(Mesure(60, T0));
            await _service.EvaluerVitesse(Mesure(60, T0.AddSeconds(3)));
            var resultat = await _service.EvaluerVitesse(Mesure(60, T0.AddSeconds(6)));

            Assert.Equal(EtatSurveillance.Declenchee, resultat.Etat);
            Assert.True(resultat.AlerteEnvoyee);
            Assert.Equal(1, _notif.NbPushPour(SeveriteAlerte.Avertissement));
            Assert.Equal(1, _notif.NbDashboard);
        }

        [Fact]
        public async Task TroisEchantillons_MaisDureeTropCourte_RestentEnObservation()
        {
            // 3 echantillons en 2 secondes : sous DureeMinimumSecondes (5).
            await _service.EvaluerVitesse(Mesure(60, T0));
            await _service.EvaluerVitesse(Mesure(60, T0.AddSeconds(1)));
            var resultat = await _service.EvaluerVitesse(Mesure(60, T0.AddSeconds(2)));

            Assert.Equal(EtatSurveillance.EnObservation, resultat.Etat);
            Assert.False(resultat.AlerteEnvoyee);
            Assert.Equal(0, _notif.NbPush);
        }

        [Fact]
        public async Task VitesseCritique_DeclencheImmediatement()
        {
            // 80 - 3 = 77 >= 75 : pas d'accumulation d'echantillons.
            var resultat = await _service.EvaluerVitesse(Mesure(80, T0));

            Assert.Equal(SeveriteAlerte.Critique, resultat.Severite);
            Assert.Equal(EtatSurveillance.Declenchee, resultat.Etat);
            Assert.True(resultat.AlerteEnvoyee);
            Assert.Equal(1, _notif.NbPushPour(SeveriteAlerte.Critique));
            Assert.Equal(1, _notif.NbSms);
            Assert.Equal(1, _notif.NbEmail);
            Assert.Equal(1, _notif.NbDashboard);
        }

        [Fact]
        public async Task RetourALaNormale_ReinitialiseLEtatInterne()
        {
            await _service.EvaluerVitesse(Mesure(80, T0));

            var retour = await _service.EvaluerVitesse(Mesure(40, T0.AddSeconds(10)));

            Assert.Equal(SeveriteAlerte.Aucune, retour.Severite);
            Assert.Equal("Retour à la normale", retour.Raison);

            // COMPORTEMENT EXISTANT, NON MODIFIE PAR CE CORRECTIF :
            // ResultatEvaluation.Etat est renseigne avant l'appel a
            // ReinitialiserEtat, il reflete donc encore l'etat precedent.
            // Le test le fige tel quel pour eviter une regression silencieuse.
            Assert.Equal(EtatSurveillance.Declenchee, retour.Etat);

            // La mesure suivante confirme que le contexte a bien ete remis a zero.
            var suivante = await _service.EvaluerVitesse(Mesure(40, T0.AddSeconds(20)));
            Assert.Equal(EtatSurveillance.Normal, suivante.Etat);
        }

        [Fact]
        public async Task AlertesRepetees_FinissentParEscalader()
        {
            var config = new ConfigurationSeuil
            {
                ToleranceGps = 0,
                EchantillonsRequis = 1,
                DureeMinimumSecondes = 0,
                CooldownMinutes = 0,
                SeuilEscalade = 3,
                MaxAlertesParHeure = 100
            };
            var service = new AlerteVitesseService(config, new StubNotifications());

            // 65 km/h : severite Alerte (>= 60, < 75), donc l'etat n'est pas
            // reecrit a chaque mesure comme il le serait en Critique.
            ResultatEvaluation dernier = null!;
            for (int i = 0; i < 5; i++)
            {
                dernier = await service.EvaluerVitesse(Mesure(65, T0.AddMinutes(i)));
            }

            Assert.Equal(EtatSurveillance.Escaladee, dernier.Etat);
        }

        [Fact]
        public async Task SeuilDepasse_CorrespondALaSeverite()
        {
            var critique = await _service.EvaluerVitesse(Mesure(80, T0));
            Assert.Equal(75.0, critique.SeuilDepasse);

            var normale = await _service.EvaluerVitesse(Mesure(40, T0.AddMinutes(1)));
            Assert.Equal(0.0, normale.SeuilDepasse);
        }

        [Fact]
        public async Task VitesseMesuree_EstLaValeurBrute_PasCorrigee()
        {
            var resultat = await _service.EvaluerVitesse(Mesure(80, T0));

            Assert.Equal(80.0, resultat.VitesseMesuree);
        }

        [Fact]
        public async Task DonneeNulle_LeveArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.EvaluerVitesse(null!));
        }
    }
}
