// ============================================================================
// GEO-16 : Service Résilience & Haute Disponibilité
// Story : En tant que système, je souhaite continuer à fonctionner en cas de
//         panne d'un composant afin d'assurer une haute disponibilité 24/7.
// Critères d'acceptation :
//   1. Une panne partielle n'interrompt pas tout le système
//   2. Un mécanisme de secours est documenté
// Epic parent : GEO-3 — Historique et tableau de bord analytique
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeoTrack.Api.Services.Resilience
{
    // ========================================================================
    // ENUMS
    // ========================================================================

    /// <summary>État du Circuit Breaker</summary>
    public enum EtatCircuitBreaker
    {
        Ferme,        // Fonctionnement normal
        Ouvert,       // Panne détectée — requêtes bloquées
        SemiOuvert    // Tentative de recovery
    }

    /// <summary>Niveau de santé d'un composant</summary>
    public enum NiveauSante
    {
        Sain,         // Tout OK
        Degrade,      // Fonctionnel mais ralenti
        Critique,     // Panne — failover actif
        Inconnu       // Pas de données
    }

    /// <summary>Type d'événement résilience</summary>
    public enum TypeEvenement
    {
        CircuitOuvert,
        CircuitFerme,
        CircuitSemiOuvert,
        RetryReussi,
        RetryEchoue,
        FailoverActive,
        FailoverDesactive,
        ComposantDemarre,
        ComposantArrete,
        HealthCheckEchoue,
        HealthCheckReussi,
        PanneDetectee,
        RecoveryComplete
    }

    /// <summary>Composants du système GeoTrack</summary>
    public enum ComposantSysteme
    {
        ApiPrincipale,
        ServiceGps,
        BaseDonnees,
        ServiceAlertes,       // GEO-9
        ServiceNotifications, // GEO-10
        ServiceCarte          // GEO-7
    }

    // ========================================================================
    // MODÈLES
    // ========================================================================

    /// <summary>Configuration Circuit Breaker par composant</summary>
    public class ConfigCircuitBreaker
    {
        public ComposantSysteme Composant { get; set; }
        public int SeuilEchecs { get; set; } = 5;
        public TimeSpan DureeOuverture { get; set; } = TimeSpan.FromSeconds(30);
        public int TentativesSemiOuvert { get; set; } = 3;
        public TimeSpan TimeoutAppel { get; set; } = TimeSpan.FromSeconds(10);
        public bool FailoverActif { get; set; } = true;
    }

    /// <summary>Configuration Retry avec backoff exponentiel</summary>
    public class ConfigRetry
    {
        public int MaxTentatives { get; set; } = 3;
        public TimeSpan DelaiInitial { get; set; } = TimeSpan.FromMilliseconds(500);
        public double FacteurBackoff { get; set; } = 2.0;
        public TimeSpan DelaiMax { get; set; } = TimeSpan.FromSeconds(30);
        public bool AjouterJitter { get; set; } = true;
    }

    /// <summary>État d'un Circuit Breaker en temps réel</summary>
    public class EtatCircuit
    {
        public ComposantSysteme Composant { get; set; }
        public EtatCircuitBreaker Etat { get; set; } = EtatCircuitBreaker.Ferme;
        public int EchecsConsecutifs { get; set; } = 0;
        public int TotalEchecs { get; set; } = 0;
        public int TotalSucces { get; set; } = 0;
        public DateTime? DernierEchec { get; set; }
        public DateTime? DernierSucces { get; set; }
        public DateTime? OuvertDepuis { get; set; }
        public DateTime? ProchaineTentative { get; set; }
        public int TentativesSemiOuvertReussies { get; set; } = 0;
    }

    /// <summary>Rapport santé d'un composant</summary>
    public class RapportSanteComposant
    {
        public ComposantSysteme Composant { get; set; }
        public NiveauSante Niveau { get; set; }
        public EtatCircuitBreaker EtatCircuit { get; set; }
        public double DisponibilitePourcentage { get; set; }
        public TimeSpan LatenceMoyenne { get; set; }
        public bool FailoverActif { get; set; }
        public string ComposantSecours { get; set; }
        public DateTime DernierCheck { get; set; }
        public string Message { get; set; }
    }

    /// <summary>Rapport santé global du système</summary>
    public class RapportSanteGlobal
    {
        public NiveauSante NiveauGlobal { get; set; }
        public int ComposantsActifs { get; set; }
        public int ComposantsTotal { get; set; }
        public double DisponibiliteGlobale { get; set; }
        public TimeSpan UptimeSysteme { get; set; }
        public List<RapportSanteComposant> Composants { get; set; } = new();
        public List<EvenementResilience> DerniersEvenements { get; set; } = new();
        public DateTime DateRapport { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Événement de résilience (timeline)</summary>
    public class EvenementResilience
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TypeEvenement Type { get; set; }
        public ComposantSysteme Composant { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Message { get; set; }
        public string Details { get; set; }
        public NiveauSante NiveauAvant { get; set; }
        public NiveauSante NiveauApres { get; set; }
    }

    /// <summary>Métriques de résilience</summary>
    public class MetriquesResilience
    {
        public ComposantSysteme Composant { get; set; }
        public long TotalRequetes { get; set; }
        public long RequetesReussies { get; set; }
        public long RequetesEchouees { get; set; }
        public long RequetesRetry { get; set; }
        public long RequetesFailover { get; set; }
        public double TauxErreur => TotalRequetes > 0 ? (double)RequetesEchouees / TotalRequetes * 100 : 0;
        public double TauxSucces => TotalRequetes > 0 ? (double)RequetesReussies / TotalRequetes * 100 : 0;
        public TimeSpan LatenceMoyenne { get; set; }
        public TimeSpan LatenceP99 { get; set; }
        public int CircuitOuvertCount { get; set; }
        public TimeSpan TempsTotalPanne { get; set; }
    }

    /// <summary>Configuration failover (composant de secours)</summary>
    public class ConfigFailover
    {
        public ComposantSysteme ComposantPrincipal { get; set; }
        public string StrategieSecours { get; set; }
        public string Description { get; set; }
        public bool Automatique { get; set; } = true;
        public TimeSpan DelaiActivation { get; set; } = TimeSpan.FromSeconds(5);
    }

    /// <summary>Résultat d'une opération résiliente</summary>
    public class ResultatResilient<T>
    {
        public bool Succes { get; set; }
        public T Donnees { get; set; }
        public string Source { get; set; } // "principal" ou "failover"
        public int TentativesEffectuees { get; set; }
        public TimeSpan DureeExecution { get; set; }
        public string Erreur { get; set; }

        public static ResultatResilient<T> Reussi(T donnees, string source, int tentatives, TimeSpan duree)
            => new() { Succes = true, Donnees = donnees, Source = source, TentativesEffectuees = tentatives, DureeExecution = duree };

        public static ResultatResilient<T> Echoue(string erreur, int tentatives, TimeSpan duree)
            => new() { Succes = false, Erreur = erreur, TentativesEffectuees = tentatives, DureeExecution = duree };
    }

    // ========================================================================
    // SERVICE RÉSILIENCE
    // ========================================================================

    public class ResilienceService
    {
        private readonly ConcurrentDictionary<ComposantSysteme, EtatCircuit> _circuits = new();
        private readonly ConcurrentDictionary<ComposantSysteme, ConfigCircuitBreaker> _configsCB = new();
        private readonly ConcurrentDictionary<ComposantSysteme, ConfigRetry> _configsRetry = new();
        private readonly ConcurrentDictionary<ComposantSysteme, ConfigFailover> _configsFailover = new();
        private readonly ConcurrentDictionary<ComposantSysteme, MetriquesResilience> _metriques = new();
        private readonly ConcurrentBag<EvenementResilience> _evenements = new();
        private readonly DateTime _demarrage = DateTime.UtcNow;
        private static readonly Random _random = new();

        public ResilienceService()
        {
            InitialiserComposants();
        }

        // ====================================================================
        // INITIALISATION
        // ====================================================================

        private void InitialiserComposants()
        {
            var composants = Enum.GetValues<ComposantSysteme>();

            foreach (var composant in composants)
            {
                _circuits[composant] = new EtatCircuit { Composant = composant };
                _configsCB[composant] = new ConfigCircuitBreaker { Composant = composant };
                _configsRetry[composant] = new ConfigRetry();
                _metriques[composant] = new MetriquesResilience { Composant = composant };
            }

            // Configuration failover spécifique par composant
            _configsFailover[ComposantSysteme.ServiceGps] = new ConfigFailover
            {
                ComposantPrincipal = ComposantSysteme.ServiceGps,
                StrategieSecours = "Cache local positions GPS (5 dernières minutes)",
                Description = "En cas de panne GPS, utiliser les dernières positions connues en cache",
                Automatique = true,
                DelaiActivation = TimeSpan.FromSeconds(3)
            };

            _configsFailover[ComposantSysteme.BaseDonnees] = new ConfigFailover
            {
                ComposantPrincipal = ComposantSysteme.BaseDonnees,
                StrategieSecours = "File d'attente locale + réplica lecture seule",
                Description = "Écriture en file locale, lecture depuis réplica. Sync auto à la recovery.",
                Automatique = true,
                DelaiActivation = TimeSpan.FromSeconds(5)
            };

            _configsFailover[ComposantSysteme.ServiceNotifications] = new ConfigFailover
            {
                ComposantPrincipal = ComposantSysteme.ServiceNotifications,
                StrategieSecours = "File d'attente notifications + retry différé",
                Description = "Notifications mises en file. Envoi batch à la recovery.",
                Automatique = true,
                DelaiActivation = TimeSpan.FromSeconds(2)
            };

            _configsFailover[ComposantSysteme.ServiceAlertes] = new ConfigFailover
            {
                ComposantPrincipal = ComposantSysteme.ServiceAlertes,
                StrategieSecours = "Alertes critiques seules via canal secondaire",
                Description = "Seules les alertes critiques (géofence + vitesse) passent par canal SMS direct.",
                Automatique = true,
                DelaiActivation = TimeSpan.FromSeconds(2)
            };

            _configsFailover[ComposantSysteme.ServiceCarte] = new ConfigFailover
            {
                ComposantPrincipal = ComposantSysteme.ServiceCarte,
                StrategieSecours = "Cache tuiles carte + positions simplifiées",
                Description = "Tuiles depuis cache CDN, positions mises à jour toutes les 30s au lieu de temps réel.",
                Automatique = true,
                DelaiActivation = TimeSpan.FromSeconds(5)
            };

            _configsFailover[ComposantSysteme.ApiPrincipale] = new ConfigFailover
            {
                ComposantPrincipal = ComposantSysteme.ApiPrincipale,
                StrategieSecours = "Instance secondaire + load balancer",
                Description = "Basculement automatique vers instance secondaire via health check LB.",
                Automatique = true,
                DelaiActivation = TimeSpan.FromSeconds(10)
            };

            EnregistrerEvenement(TypeEvenement.ComposantDemarre, ComposantSysteme.ApiPrincipale,
                "Système GeoTrack démarré — tous composants initialisés");
        }

        // ====================================================================
        // EXÉCUTION RÉSILIENTE (CŒUR DU SYSTÈME)
        // ====================================================================

        /// <summary>
        /// Exécute une opération avec Circuit Breaker + Retry + Failover.
        /// C'est le point d'entrée principal pour toute opération résiliente.
        /// </summary>
        public async Task<ResultatResilient<T>> ExecuterAvecResilience<T>(
            ComposantSysteme composant,
            Func<Task<T>> operationPrincipale,
            Func<Task<T>> operationFailover = null)
        {
            var debut = DateTime.UtcNow;
            var metriques = _metriques[composant];
            Interlocked.Increment(ref metriques.TotalRequetes);

            // 1. Vérifier état Circuit Breaker
            var circuit = _circuits[composant];
            if (circuit.Etat == EtatCircuitBreaker.Ouvert)
            {
                // Vérifier si on peut passer en semi-ouvert
                if (DateTime.UtcNow >= circuit.ProchaineTentative)
                {
                    PasserSemiOuvert(composant);
                }
                else
                {
                    // Circuit ouvert — tenter failover directement
                    return await TenterFailover(composant, operationFailover, debut);
                }
            }

            // 2. Tenter opération principale avec retry
            var configRetry = _configsRetry[composant];
            var tentative = 0;
            Exception derniereException = null;

            while (tentative < configRetry.MaxTentatives)
            {
                tentative++;
                try
                {
                    var resultat = await operationPrincipale();

                    // Succès !
                    EnregistrerSucces(composant);
                    var duree = DateTime.UtcNow - debut;

                    if (tentative > 1)
                    {
                        Interlocked.Increment(ref metriques.RequetesRetry);
                        EnregistrerEvenement(TypeEvenement.RetryReussi, composant,
                            $"Retry réussi à la tentative {tentative}");
                    }

                    return ResultatResilient<T>.Reussi(resultat, "principal", tentative, duree);
                }
                catch (Exception ex)
                {
                    derniereException = ex;

                    if (tentative < configRetry.MaxTentatives)
                    {
                        // Attendre avec backoff exponentiel + jitter
                        var delai = CalculerDelaiBackoff(configRetry, tentative);
                        await Task.Delay(delai);
                    }
                }
            }

            // 3. Toutes tentatives échouées — enregistrer échec
            EnregistrerEchec(composant);

            // 4. Vérifier si Circuit Breaker doit s'ouvrir
            if (circuit.EchecsConsecutifs >= _configsCB[composant].SeuilEchecs)
            {
                OuvrirCircuit(composant);
            }

            // 5. Tenter failover
            return await TenterFailover(composant, operationFailover, debut);
        }

        // ====================================================================
        // CIRCUIT BREAKER
        // ====================================================================

        private void OuvrirCircuit(ComposantSysteme composant)
        {
            var circuit = _circuits[composant];
            var config = _configsCB[composant];

            circuit.Etat = EtatCircuitBreaker.Ouvert;
            circuit.OuvertDepuis = DateTime.UtcNow;
            circuit.ProchaineTentative = DateTime.UtcNow.Add(config.DureeOuverture);
            circuit.TentativesSemiOuvertReussies = 0;

            EnregistrerEvenement(TypeEvenement.CircuitOuvert, composant,
                $"Circuit Breaker OUVERT après {circuit.EchecsConsecutifs} échecs consécutifs. " +
                $"Prochaine tentative dans {config.DureeOuverture.TotalSeconds}s.");

            // Activer failover automatique
            if (config.FailoverActif && _configsFailover.ContainsKey(composant))
            {
                EnregistrerEvenement(TypeEvenement.FailoverActive, composant,
                    $"Failover activé : {_configsFailover[composant].StrategieSecours}");
            }
        }

        private void PasserSemiOuvert(ComposantSysteme composant)
        {
            var circuit = _circuits[composant];
            circuit.Etat = EtatCircuitBreaker.SemiOuvert;
            circuit.TentativesSemiOuvertReussies = 0;

            EnregistrerEvenement(TypeEvenement.CircuitSemiOuvert, composant,
                "Circuit Breaker SEMI-OUVERT — tentative de recovery en cours");
        }

        private void FermerCircuit(ComposantSysteme composant)
        {
            var circuit = _circuits[composant];
            circuit.Etat = EtatCircuitBreaker.Ferme;
            circuit.EchecsConsecutifs = 0;
            circuit.OuvertDepuis = null;
            circuit.ProchaineTentative = null;

            EnregistrerEvenement(TypeEvenement.CircuitFerme, composant,
                "Circuit Breaker FERMÉ — composant récupéré, fonctionnement normal");

            if (_configsFailover.ContainsKey(composant))
            {
                EnregistrerEvenement(TypeEvenement.FailoverDesactive, composant,
                    "Failover désactivé — retour au composant principal");
            }
        }

        // ====================================================================
        // RETRY AVEC BACKOFF EXPONENTIEL
        // ====================================================================

        private TimeSpan CalculerDelaiBackoff(ConfigRetry config, int tentative)
        {
            var delai = config.DelaiInitial.TotalMilliseconds * Math.Pow(config.FacteurBackoff, tentative - 1);
            delai = Math.Min(delai, config.DelaiMax.TotalMilliseconds);

            if (config.AjouterJitter)
            {
                // Jitter ±25% pour éviter thundering herd
                var jitter = delai * 0.25 * (2 * _random.NextDouble() - 1);
                delai += jitter;
            }

            return TimeSpan.FromMilliseconds(Math.Max(0, delai));
        }

        // ====================================================================
        // FAILOVER
        // ====================================================================

        private async Task<ResultatResilient<T>> TenterFailover<T>(
            ComposantSysteme composant,
            Func<Task<T>> operationFailover,
            DateTime debut)
        {
            var metriques = _metriques[composant];

            if (operationFailover != null && _configsFailover.ContainsKey(composant))
            {
                try
                {
                    var resultat = await operationFailover();
                    Interlocked.Increment(ref metriques.RequetesFailover);
                    var duree = DateTime.UtcNow - debut;

                    return ResultatResilient<T>.Reussi(resultat, "failover", 0, duree);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref metriques.RequetesEchouees);
                    var duree = DateTime.UtcNow - debut;
                    return ResultatResilient<T>.Echoue(
                        $"Échec principal ET failover : {ex.Message}", 0, duree);
                }
            }

            Interlocked.Increment(ref metriques.RequetesEchouees);
            var dureeTotale = DateTime.UtcNow - debut;
            return ResultatResilient<T>.Echoue("Échec — aucun failover configuré", 0, dureeTotale);
        }

        // ====================================================================
        // ENREGISTREMENT SUCCÈS / ÉCHEC
        // ====================================================================

        private void EnregistrerSucces(ComposantSysteme composant)
        {
            var circuit = _circuits[composant];
            var metriques = _metriques[composant];

            circuit.DernierSucces = DateTime.UtcNow;
            circuit.EchecsConsecutifs = 0;
            Interlocked.Increment(ref circuit.TotalSucces);
            Interlocked.Increment(ref metriques.RequetesReussies);

            // Si semi-ouvert, compter les succès pour fermer le circuit
            if (circuit.Etat == EtatCircuitBreaker.SemiOuvert)
            {
                circuit.TentativesSemiOuvertReussies++;
                if (circuit.TentativesSemiOuvertReussies >= _configsCB[composant].TentativesSemiOuvert)
                {
                    FermerCircuit(composant);
                }
            }
        }

        private void EnregistrerEchec(ComposantSysteme composant)
        {
            var circuit = _circuits[composant];
            circuit.DernierEchec = DateTime.UtcNow;
            circuit.EchecsConsecutifs++;
            Interlocked.Increment(ref circuit.TotalEchecs);
        }

        // ====================================================================
        // HEALTH CHECKS
        // ====================================================================

        /// <summary>Vérifier la santé de tous les composants</summary>
        public async Task<RapportSanteGlobal> ObtenirRapportSante()
        {
            var rapports = new List<RapportSanteComposant>();
            var composants = Enum.GetValues<ComposantSysteme>();

            foreach (var composant in composants)
            {
                rapports.Add(GenererRapportComposant(composant));
            }

            var composantsActifs = rapports.Count(r => r.Niveau != NiveauSante.Critique);
            var disponibiliteGlobale = rapports.Average(r => r.DisponibilitePourcentage);

            var niveauGlobal = composantsActifs == composants.Length ? NiveauSante.Sain
                : composantsActifs >= composants.Length - 1 ? NiveauSante.Degrade
                : NiveauSante.Critique;

            return new RapportSanteGlobal
            {
                NiveauGlobal = niveauGlobal,
                ComposantsActifs = composantsActifs,
                ComposantsTotal = composants.Length,
                DisponibiliteGlobale = Math.Round(disponibiliteGlobale, 2),
                UptimeSysteme = DateTime.UtcNow - _demarrage,
                Composants = rapports,
                DerniersEvenements = _evenements
                    .OrderByDescending(e => e.Timestamp)
                    .Take(20)
                    .ToList()
            };
        }

        private RapportSanteComposant GenererRapportComposant(ComposantSysteme composant)
        {
            var circuit = _circuits[composant];
            var metriques = _metriques[composant];

            var niveau = circuit.Etat switch
            {
                EtatCircuitBreaker.Ferme => NiveauSante.Sain,
                EtatCircuitBreaker.SemiOuvert => NiveauSante.Degrade,
                EtatCircuitBreaker.Ouvert => NiveauSante.Critique,
                _ => NiveauSante.Inconnu
            };

            var failoverActif = circuit.Etat == EtatCircuitBreaker.Ouvert
                && _configsFailover.ContainsKey(composant);

            return new RapportSanteComposant
            {
                Composant = composant,
                Niveau = niveau,
                EtatCircuit = circuit.Etat,
                DisponibilitePourcentage = metriques.TauxSucces > 0 ? metriques.TauxSucces : 99.9,
                LatenceMoyenne = metriques.LatenceMoyenne,
                FailoverActif = failoverActif,
                ComposantSecours = failoverActif ? _configsFailover[composant].StrategieSecours : null,
                DernierCheck = DateTime.UtcNow,
                Message = GenererMessageSante(composant, niveau)
            };
        }

        private string GenererMessageSante(ComposantSysteme composant, NiveauSante niveau)
        {
            return niveau switch
            {
                NiveauSante.Sain => $"{composant} : fonctionnement normal",
                NiveauSante.Degrade => $"{composant} : dégradé — recovery en cours",
                NiveauSante.Critique => $"{composant} : PANNE — failover actif",
                _ => $"{composant} : état inconnu"
            };
        }

        // ====================================================================
        // OBTENIR DÉTAILS CIRCUIT BREAKERS
        // ====================================================================

        /// <summary>Obtenir l'état de tous les Circuit Breakers</summary>
        public List<EtatCircuit> ObtenirEtatsCircuits()
        {
            return _circuits.Values.ToList();
        }

        /// <summary>Obtenir les métriques de tous les composants</summary>
        public List<MetriquesResilience> ObtenirMetriques()
        {
            return _metriques.Values.ToList();
        }

        /// <summary>Obtenir les configurations failover</summary>
        public List<ConfigFailover> ObtenirConfigsFailover()
        {
            return _configsFailover.Values.ToList();
        }

        /// <summary>Obtenir la timeline des événements</summary>
        public List<EvenementResilience> ObtenirEvenements(int limite = 50)
        {
            return _evenements
                .OrderByDescending(e => e.Timestamp)
                .Take(limite)
                .ToList();
        }

        // ====================================================================
        // RESET MANUEL (ADMIN)
        // ====================================================================

        /// <summary>Forcer la fermeture d'un circuit (reset admin)</summary>
        public void ResetCircuit(ComposantSysteme composant)
        {
            FermerCircuit(composant);
            EnregistrerEvenement(TypeEvenement.RecoveryComplete, composant,
                "Circuit reseté manuellement par administrateur");
        }

        // ====================================================================
        // ÉVÉNEMENTS
        // ====================================================================

        private void EnregistrerEvenement(TypeEvenement type, ComposantSysteme composant, string message)
        {
            _evenements.Add(new EvenementResilience
            {
                Type = type,
                Composant = composant,
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    // ========================================================================
    // CONTRÔLEUR API HEALTH & RÉSILIENCE
    // ========================================================================

    // [ApiController]
    // [Route("api/health")]
    public class HealthController
    {
        private readonly ResilienceService _resilience;

        public HealthController(ResilienceService resilience)
        {
            _resilience = resilience;
        }

        // GET /api/health
        // Critère #1 : vérifier qu'une panne partielle n'interrompt pas tout
        public async Task<RapportSanteGlobal> ObtenirSanteGlobale()
        {
            return await _resilience.ObtenirRapportSante();
        }

        // GET /api/health/composants
        public List<EtatCircuit> ObtenirCircuitBreakers()
        {
            return _resilience.ObtenirEtatsCircuits();
        }

        // GET /api/health/metriques
        public List<MetriquesResilience> ObtenirMetriques()
        {
            return _resilience.ObtenirMetriques();
        }

        // GET /api/health/failover
        // Critère #2 : mécanismes de secours documentés
        public List<ConfigFailover> ObtenirFailovers()
        {
            return _resilience.ObtenirConfigsFailover();
        }

        // GET /api/health/evenements?limite=50
        public List<EvenementResilience> ObtenirEvenements(int limite = 50)
        {
            return _resilience.ObtenirEvenements(limite);
        }

        // POST /api/health/reset/{composant}
        public void ResetCircuit(ComposantSysteme composant)
        {
            _resilience.ResetCircuit(composant);
        }
    }

    // ========================================================================
    // EXEMPLES D'UTILISATION AVEC LES AUTRES SERVICES GEOTRACK
    // ========================================================================

    /// <summary>
    /// Exemple : Service GPS résilient (intégration GEO-9/GEO-10)
    /// Démontre comment les autres services utilisent ResilienceService
    /// </summary>
    public class ServiceGpsResilient
    {
        private readonly ResilienceService _resilience;

        // Cache local pour failover GPS
        private readonly ConcurrentDictionary<string, object> _cachePositions = new();

        public ServiceGpsResilient(ResilienceService resilience)
        {
            _resilience = resilience;
        }

        /// <summary>
        /// Obtenir position véhicule avec résilience complète.
        /// Si le service GPS est en panne, retourne la dernière position connue (cache).
        /// </summary>
        public async Task<ResultatResilient<object>> ObtenirPositionVehicule(string vehiculeId)
        {
            return await _resilience.ExecuterAvecResilience(
                ComposantSysteme.ServiceGps,
                // Opération principale
                async () =>
                {
                    // Appel service GPS réel
                    var position = await AppelerServiceGps(vehiculeId);
                    // Mettre en cache pour failover
                    _cachePositions[vehiculeId] = position;
                    return position;
                },
                // Opération failover
                async () =>
                {
                    // Retourner dernière position connue
                    if (_cachePositions.TryGetValue(vehiculeId, out var cached))
                    {
                        return cached;
                    }
                    throw new Exception("Aucune position en cache");
                }
            );
        }

        private Task<object> AppelerServiceGps(string vehiculeId)
        {
            // Simulation appel service GPS
            return Task.FromResult<object>(new { VehiculeId = vehiculeId, Lat = 45.4765, Lng = -75.7013 });
        }
    }

    /// <summary>
    /// Exemple : Service Notifications résilient (GEO-10)
    /// File d'attente locale si le service est en panne
    /// </summary>
    public class ServiceNotificationsResilient
    {
        private readonly ResilienceService _resilience;
        private readonly ConcurrentQueue<object> _fileAttente = new();

        public ServiceNotificationsResilient(ResilienceService resilience)
        {
            _resilience = resilience;
        }

        public async Task<ResultatResilient<bool>> EnvoyerNotification(object notification)
        {
            return await _resilience.ExecuterAvecResilience(
                ComposantSysteme.ServiceNotifications,
                async () =>
                {
                    await EnvoyerViaServicePrincipal(notification);
                    return true;
                },
                async () =>
                {
                    // Failover : mettre en file d'attente
                    _fileAttente.Enqueue(notification);
                    return true; // Notification sera envoyée à la recovery
                }
            );
        }

        /// <summary>Vider la file d'attente après recovery</summary>
        public async Task TraiterFileAttente()
        {
            while (_fileAttente.TryDequeue(out var notification))
            {
                await EnvoyerViaServicePrincipal(notification);
            }
        }

        private Task EnvoyerViaServicePrincipal(object notification)
        {
            return Task.CompletedTask; // Simulation
        }
    }
}
