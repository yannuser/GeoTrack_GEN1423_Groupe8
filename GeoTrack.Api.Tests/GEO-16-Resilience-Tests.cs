using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace GeoTrack.Api.Tests
{
    // ============================================================
    // GEO-16 : Tests Unitaires — Résilience & Haute Disponibilité
    // Story : En tant que système, je souhaite continuer à fonctionner
    //         en cas de panne d'un composant afin d'assurer une haute
    //         disponibilité 24/7.
    // Critères :
    //   1. Une panne partielle n'interrompt pas tout le système
    //   2. Un mécanisme de secours est documenté
    // ============================================================

    #region Enums et Modèles (copie pour tests)

    public enum EtatCircuitBreaker { Ferme, Ouvert, SemiOuvert }
    public enum NiveauSante { Sain, Degrade, Critique, HorsService }

    public class ConfigCircuitBreaker
    {
        public string NomComposant { get; set; }
        public int SeuilEchecs { get; set; } = 5;
        public TimeSpan DelaiRecovery { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan FenetreEvaluation { get; set; } = TimeSpan.FromMinutes(1);
    }

    public class ConfigRetry
    {
        public int NombreMaxTentatives { get; set; } = 3;
        public TimeSpan DelaiInitial { get; set; } = TimeSpan.FromMilliseconds(500);
        public double FacteurBackoff { get; set; } = 2.0;
        public double JitterPourcentage { get; set; } = 0.25;
    }

    public class ConfigFailover
    {
        public string ComposantPrincipal { get; set; }
        public string StrategieSecours { get; set; }
        public string Description { get; set; }
        public TimeSpan DelaiBasculement { get; set; } = TimeSpan.FromSeconds(5);
    }

    public class EtatCircuit
    {
        public string NomComposant { get; set; }
        public EtatCircuitBreaker Etat { get; set; }
        public int NombreEchecsConsecutifs { get; set; }
        public DateTime? DernierEchec { get; set; }
        public DateTime? ProchaineTentative { get; set; }
    }

    public class RapportSante
    {
        public NiveauSante NiveauGlobal { get; set; }
        public int ComposantsActifs { get; set; }
        public int ComposantsTotal { get; set; }
        public double DisponibilitePourcentage { get; set; }
        public List<RapportComposant> Composants { get; set; } = new();
        public DateTime DateRapport { get; set; }
    }

    public class RapportComposant
    {
        public string Nom { get; set; }
        public NiveauSante Niveau { get; set; }
        public EtatCircuitBreaker EtatCB { get; set; }
        public double LatenceMs { get; set; }
        public bool EnFailover { get; set; }
        public string StrategieActive { get; set; }
    }

    public class EvenementResilience
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string Composant { get; set; }
        public string Message { get; set; }
        public NiveauSante NiveauAvant { get; set; }
        public NiveauSante NiveauApres { get; set; }
    }

    public class ResultatExecution<T>
    {
        public bool Succes { get; set; }
        public T Valeur { get; set; }
        public string ComposantSource { get; set; }
        public bool UtiliseFailover { get; set; }
        public int TentativesEffectuees { get; set; }
        public string MessageErreur { get; set; }
        public TimeSpan TempsExecution { get; set; }
    }

    #endregion

    #region Service Résilience Simplifié (pour tests)

    public class ResilienceService
    {
        private readonly Dictionary<string, EtatCircuit> _circuits = new();
        private readonly Dictionary<string, ConfigCircuitBreaker> _configsCB = new();
        private readonly Dictionary<string, ConfigRetry> _configsRetry = new();
        private readonly Dictionary<string, ConfigFailover> _configsFailover = new();
        private readonly List<EvenementResilience> _evenements = new();
        private DateTime _dateDemarrage;

        public ResilienceService()
        {
            _dateDemarrage = DateTime.UtcNow;
            InitialiserComposants();
        }

        private void InitialiserComposants()
        {
            var composants = new[] { "API", "GPS", "BaseDonnees", "Alertes", "Notifications", "CarteFiltres" };

            foreach (var composant in composants)
            {
                _configsCB[composant] = new ConfigCircuitBreaker
                {
                    NomComposant = composant,
                    SeuilEchecs = 5,
                    DelaiRecovery = TimeSpan.FromSeconds(30)
                };

                _configsRetry[composant] = new ConfigRetry
                {
                    NombreMaxTentatives = 3,
                    DelaiInitial = TimeSpan.FromMilliseconds(500),
                    FacteurBackoff = 2.0
                };

                _circuits[composant] = new EtatCircuit
                {
                    NomComposant = composant,
                    Etat = EtatCircuitBreaker.Ferme,
                    NombreEchecsConsecutifs = 0
                };
            }

            _configsFailover["GPS"] = new ConfigFailover
            {
                ComposantPrincipal = "GPS",
                StrategieSecours = "Cache dernières positions connues",
                Description = "Utiliser cache local 5 min positions",
                DelaiBasculement = TimeSpan.FromSeconds(3)
            };

            _configsFailover["BaseDonnees"] = new ConfigFailover
            {
                ComposantPrincipal = "BaseDonnees",
                StrategieSecours = "Réplica lecture seule",
                Description = "Basculer vers réplica read-only",
                DelaiBasculement = TimeSpan.FromSeconds(5)
            };

            _configsFailover["Notifications"] = new ConfigFailover
            {
                ComposantPrincipal = "Notifications",
                StrategieSecours = "File d'attente locale",
                Description = "Stocker notifications en file locale",
                DelaiBasculement = TimeSpan.FromSeconds(2)
            };

            _configsFailover["Alertes"] = new ConfigFailover
            {
                ComposantPrincipal = "Alertes",
                StrategieSecours = "Mode dégradé - log uniquement",
                Description = "Logger alertes sans envoi temps réel",
                DelaiBasculement = TimeSpan.FromSeconds(2)
            };

            _configsFailover["CarteFiltres"] = new ConfigFailover
            {
                ComposantPrincipal = "CarteFiltres",
                StrategieSecours = "Affichage sans filtres",
                Description = "Carte avec tous véhicules sans filtre",
                DelaiBasculement = TimeSpan.FromSeconds(1)
            };

            _configsFailover["API"] = new ConfigFailover
            {
                ComposantPrincipal = "API",
                StrategieSecours = "Instance secondaire",
                Description = "Rediriger vers instance API backup",
                DelaiBasculement = TimeSpan.FromSeconds(10)
            };
        }

        public async Task<ResultatExecution<T>> ExecuterAvecResilience<T>(
            string nomComposant,
            Func<Task<T>> operationPrincipale,
            Func<Task<T>> operationSecours = null)
        {
            var debut = DateTime.UtcNow;
            var circuit = _circuits[nomComposant];
            var configRetry = _configsRetry[nomComposant];

            // Vérifier Circuit Breaker
            if (circuit.Etat == EtatCircuitBreaker.Ouvert)
            {
                if (circuit.ProchaineTentative.HasValue && DateTime.UtcNow >= circuit.ProchaineTentative.Value)
                {
                    circuit.Etat = EtatCircuitBreaker.SemiOuvert;
                    EnregistrerEvenement("CircuitSemiOuvert", nomComposant, "Tentative recovery");
                }
                else
                {
                    // Utiliser failover
                    if (operationSecours != null)
                    {
                        var resultatSecours = await operationSecours();
                        return new ResultatExecution<T>
                        {
                            Succes = true,
                            Valeur = resultatSecours,
                            ComposantSource = nomComposant,
                            UtiliseFailover = true,
                            TentativesEffectuees = 0,
                            TempsExecution = DateTime.UtcNow - debut
                        };
                    }

                    return new ResultatExecution<T>
                    {
                        Succes = false,
                        ComposantSource = nomComposant,
                        UtiliseFailover = false,
                        MessageErreur = $"Circuit ouvert pour {nomComposant}, pas de failover disponible",
                        TempsExecution = DateTime.UtcNow - debut
                    };
                }
            }

            // Tentatives avec Retry
            for (int tentative = 1; tentative <= configRetry.NombreMaxTentatives; tentative++)
            {
                try
                {
                    var resultat = await operationPrincipale();
                    // Succès → reset compteur
                    circuit.NombreEchecsConsecutifs = 0;
                    if (circuit.Etat == EtatCircuitBreaker.SemiOuvert)
                    {
                        circuit.Etat = EtatCircuitBreaker.Ferme;
                        EnregistrerEvenement("CircuitFerme", nomComposant, "Recovery réussi");
                    }

                    return new ResultatExecution<T>
                    {
                        Succes = true,
                        Valeur = resultat,
                        ComposantSource = nomComposant,
                        UtiliseFailover = false,
                        TentativesEffectuees = tentative,
                        TempsExecution = DateTime.UtcNow - debut
                    };
                }
                catch (Exception ex)
                {
                    circuit.NombreEchecsConsecutifs++;
                    circuit.DernierEchec = DateTime.UtcNow;

                    if (circuit.NombreEchecsConsecutifs >= _configsCB[nomComposant].SeuilEchecs)
                    {
                        circuit.Etat = EtatCircuitBreaker.Ouvert;
                        circuit.ProchaineTentative = DateTime.UtcNow.Add(_configsCB[nomComposant].DelaiRecovery);
                        EnregistrerEvenement("CircuitOuvert", nomComposant, $"Seuil atteint: {circuit.NombreEchecsConsecutifs} échecs");

                        // Activer failover
                        if (operationSecours != null)
                        {
                            EnregistrerEvenement("FailoverActive", nomComposant, _configsFailover.ContainsKey(nomComposant) ? _configsFailover[nomComposant].StrategieSecours : "Secours générique");
                            var resultatSecours = await operationSecours();
                            return new ResultatExecution<T>
                            {
                                Succes = true,
                                Valeur = resultatSecours,
                                ComposantSource = nomComposant,
                                UtiliseFailover = true,
                                TentativesEffectuees = tentative,
                                TempsExecution = DateTime.UtcNow - debut
                            };
                        }

                        return new ResultatExecution<T>
                        {
                            Succes = false,
                            ComposantSource = nomComposant,
                            MessageErreur = $"Circuit ouvert après {tentative} tentatives: {ex.Message}",
                            TentativesEffectuees = tentative,
                            TempsExecution = DateTime.UtcNow - debut
                        };
                    }

                    if (tentative < configRetry.NombreMaxTentatives)
                    {
                        var delai = configRetry.DelaiInitial * Math.Pow(configRetry.FacteurBackoff, tentative - 1);
                        await Task.Delay(TimeSpan.FromMilliseconds(1)); // Simulé pour tests
                    }
                }
            }

            return new ResultatExecution<T>
            {
                Succes = false,
                ComposantSource = nomComposant,
                MessageErreur = "Toutes les tentatives épuisées",
                TentativesEffectuees = configRetry.NombreMaxTentatives,
                TempsExecution = DateTime.UtcNow - debut
            };
        }

        public RapportSante ObtenirRapportSante()
        {
            var composants = _circuits.Select(c => new RapportComposant
            {
                Nom = c.Key,
                Niveau = c.Value.Etat == EtatCircuitBreaker.Ferme ? NiveauSante.Sain :
                         c.Value.Etat == EtatCircuitBreaker.SemiOuvert ? NiveauSante.Degrade :
                         NiveauSante.Critique,
                EtatCB = c.Value.Etat,
                LatenceMs = c.Value.Etat == EtatCircuitBreaker.Ferme ? 45 : 0,
                EnFailover = c.Value.Etat == EtatCircuitBreaker.Ouvert && _configsFailover.ContainsKey(c.Key),
                StrategieActive = c.Value.Etat == EtatCircuitBreaker.Ouvert && _configsFailover.ContainsKey(c.Key)
                    ? _configsFailover[c.Key].StrategieSecours : null
            }).ToList();

            var actifs = composants.Count(c => c.Niveau != NiveauSante.HorsService);
            var total = composants.Count;

            return new RapportSante
            {
                NiveauGlobal = actifs == total ? NiveauSante.Sain :
                               actifs >= total - 1 ? NiveauSante.Degrade :
                               actifs >= total / 2 ? NiveauSante.Critique :
                               NiveauSante.HorsService,
                ComposantsActifs = actifs,
                ComposantsTotal = total,
                DisponibilitePourcentage = (double)actifs / total * 100,
                Composants = composants,
                DateRapport = DateTime.UtcNow
            };
        }

        public EtatCircuit ObtenirEtatCircuit(string nomComposant)
        {
            return _circuits.ContainsKey(nomComposant) ? _circuits[nomComposant] : null;
        }

        public void SimulerPanne(string nomComposant)
        {
            if (_circuits.ContainsKey(nomComposant))
            {
                var circuit = _circuits[nomComposant];
                circuit.Etat = EtatCircuitBreaker.Ouvert;
                circuit.NombreEchecsConsecutifs = _configsCB[nomComposant].SeuilEchecs;
                circuit.DernierEchec = DateTime.UtcNow;
                circuit.ProchaineTentative = DateTime.UtcNow.Add(_configsCB[nomComposant].DelaiRecovery);
                EnregistrerEvenement("PanneDetectee", nomComposant, "Panne simulée");
                EnregistrerEvenement("CircuitOuvert", nomComposant, "Circuit ouvert suite à panne");

                if (_configsFailover.ContainsKey(nomComposant))
                {
                    EnregistrerEvenement("FailoverActive", nomComposant, _configsFailover[nomComposant].StrategieSecours);
                }
            }
        }

        public void ResetComposant(string nomComposant)
        {
            if (_circuits.ContainsKey(nomComposant))
            {
                _circuits[nomComposant].Etat = EtatCircuitBreaker.Ferme;
                _circuits[nomComposant].NombreEchecsConsecutifs = 0;
                _circuits[nomComposant].DernierEchec = null;
                _circuits[nomComposant].ProchaineTentative = null;
                EnregistrerEvenement("ComposantReset", nomComposant, "Reset manuel");
            }
        }

        public List<EvenementResilience> ObtenirEvenements(int limite = 50)
        {
            return _evenements.OrderByDescending(e => e.Timestamp).Take(limite).ToList();
        }

        public Dictionary<string, ConfigFailover> ObtenirStrategiesFailover()
        {
            return _configsFailover;
        }

        public int NombreComposantsActifs()
        {
            return _circuits.Count(c => c.Value.Etat != EtatCircuitBreaker.Ouvert);
        }

        public int NombreComposantsTotal()
        {
            return _circuits.Count;
        }

        private void EnregistrerEvenement(string type, string composant, string message)
        {
            var niveauAvant = _circuits[composant].Etat == EtatCircuitBreaker.Ferme ? NiveauSante.Sain : NiveauSante.Critique;
            _evenements.Add(new EvenementResilience
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Composant = composant,
                Message = message,
                NiveauAvant = niveauAvant,
                NiveauApres = type.Contains("Ouvert") || type.Contains("Panne") ? NiveauSante.Critique : NiveauSante.Sain
            });
        }
    }

    #endregion

    // ================================================================
    // TESTS UNITAIRES
    // ================================================================

    public class GEO16_CircuitBreakerTests
    {
        private readonly ResilienceService _service;

        public GEO16_CircuitBreakerTests()
        {
            _service = new ResilienceService();
        }

        // --- CIRCUIT BREAKER ---

        [Fact]
        public void Circuit_EtatInitial_TousComposantsFermes()
        {
            // Arrange & Act
            var composants = new[] { "API", "GPS", "BaseDonnees", "Alertes", "Notifications", "CarteFiltres" };

            // Assert
            foreach (var composant in composants)
            {
                var etat = _service.ObtenirEtatCircuit(composant);
                Assert.NotNull(etat);
                Assert.Equal(EtatCircuitBreaker.Ferme, etat.Etat);
                Assert.Equal(0, etat.NombreEchecsConsecutifs);
            }
        }

        [Fact]
        public void Circuit_6Composants_TousInitialises()
        {
            // Act
            var actifs = _service.NombreComposantsActifs();
            var total = _service.NombreComposantsTotal();

            // Assert
            Assert.Equal(6, total);
            Assert.Equal(6, actifs);
        }

        [Fact]
        public async Task Circuit_OperationReussie_ResteeFerme()
        {
            // Arrange
            Func<Task<string>> operation = () => Task.FromResult("OK");

            // Act
            var resultat = await _service.ExecuterAvecResilience("GPS", operation);

            // Assert
            Assert.True(resultat.Succes);
            Assert.Equal("OK", resultat.Valeur);
            Assert.False(resultat.UtiliseFailover);
            Assert.Equal(1, resultat.TentativesEffectuees);

            var etat = _service.ObtenirEtatCircuit("GPS");
            Assert.Equal(EtatCircuitBreaker.Ferme, etat.Etat);
        }

        [Fact]
        public void Circuit_SimulerPanne_PasseOuvert()
        {
            // Act
            _service.SimulerPanne("GPS");

            // Assert
            var etat = _service.ObtenirEtatCircuit("GPS");
            Assert.Equal(EtatCircuitBreaker.Ouvert, etat.Etat);
            Assert.Equal(5, etat.NombreEchecsConsecutifs);
            Assert.NotNull(etat.DernierEchec);
            Assert.NotNull(etat.ProchaineTentative);
        }

        [Fact]
        public void Circuit_Reset_RevientFerme()
        {
            // Arrange
            _service.SimulerPanne("BaseDonnees");

            // Act
            _service.ResetComposant("BaseDonnees");

            // Assert
            var etat = _service.ObtenirEtatCircuit("BaseDonnees");
            Assert.Equal(EtatCircuitBreaker.Ferme, etat.Etat);
            Assert.Equal(0, etat.NombreEchecsConsecutifs);
            Assert.Null(etat.DernierEchec);
        }

        [Fact]
        public void Circuit_ComposantInexistant_RetourneNull()
        {
            // Act
            var etat = _service.ObtenirEtatCircuit("ComposantFictif");

            // Assert
            Assert.Null(etat);
        }
    }

    public class GEO16_PannePartielleTests
    {
        private readonly ResilienceService _service;

        public GEO16_PannePartielleTests()
        {
            _service = new ResilienceService();
        }

        // --- CRITÈRE #1 : PANNE PARTIELLE ≠ ARRÊT TOTAL ---

        [Fact]
        public void PannePartielle_1Composant_5RestentsActifs()
        {
            // Arrange & Act
            _service.SimulerPanne("GPS");

            // Assert
            Assert.Equal(5, _service.NombreComposantsActifs());
            Assert.Equal(6, _service.NombreComposantsTotal());
        }

        [Fact]
        public void PannePartielle_2Composants_4RestentsActifs()
        {
            // Arrange & Act
            _service.SimulerPanne("GPS");
            _service.SimulerPanne("Notifications");

            // Assert
            Assert.Equal(4, _service.NombreComposantsActifs());
        }

        [Fact]
        public void PannePartielle_NiveauDegrade_PasHorsService()
        {
            // Arrange
            _service.SimulerPanne("Alertes");

            // Act
            var rapport = _service.ObtenirRapportSante();

            // Assert
            Assert.Equal(NiveauSante.Degrade, rapport.NiveauGlobal);
            Assert.NotEqual(NiveauSante.HorsService, rapport.NiveauGlobal);
            Assert.Equal(5, rapport.ComposantsActifs);
        }

        [Fact]
        public void PannePartielle_3Composants_NiveauCritiquePasHorsService()
        {
            // Arrange
            _service.SimulerPanne("GPS");
            _service.SimulerPanne("Notifications");
            _service.SimulerPanne("Alertes");

            // Act
            var rapport = _service.ObtenirRapportSante();

            // Assert — 3 actifs sur 6 = 50% → Critique mais pas HorsService
            Assert.Equal(NiveauSante.Critique, rapport.NiveauGlobal);
            Assert.Equal(3, rapport.ComposantsActifs);
            Assert.True(rapport.DisponibilitePourcentage >= 50);
        }

        [Fact]
        public async Task PannePartielle_GPSEnPanne_AutresComposantsFonctionnent()
        {
            // Arrange
            _service.SimulerPanne("GPS");
            Func<Task<string>> operationAlerte = () => Task.FromResult("Alerte envoyée");

            // Act — les alertes doivent fonctionner normalement
            var resultat = await _service.ExecuterAvecResilience("Alertes", operationAlerte);

            // Assert
            Assert.True(resultat.Succes);
            Assert.Equal("Alerte envoyée", resultat.Valeur);
            Assert.False(resultat.UtiliseFailover);
        }

        [Fact]
        public void PannePartielle_DisponibiliteCalculee()
        {
            // Arrange
            _service.SimulerPanne("Notifications");

            // Act
            var rapport = _service.ObtenirRapportSante();

            // Assert — 5/6 = 83.33%
            Assert.True(rapport.DisponibilitePourcentage > 80);
            Assert.True(rapport.DisponibilitePourcentage < 90);
        }
    }

    public class GEO16_FailoverTests
    {
        private readonly ResilienceService _service;

        public GEO16_FailoverTests()
        {
            _service = new ResilienceService();
        }

        // --- CRITÈRE #2 : MÉCANISME DE SECOURS ---

        [Fact]
        public void Failover_6StrategiesDefinies()
        {
            // Act
            var strategies = _service.ObtenirStrategiesFailover();

            // Assert
            Assert.Equal(6, strategies.Count);
            Assert.Contains("GPS", strategies.Keys);
            Assert.Contains("BaseDonnees", strategies.Keys);
            Assert.Contains("Notifications", strategies.Keys);
            Assert.Contains("Alertes", strategies.Keys);
            Assert.Contains("CarteFiltres", strategies.Keys);
            Assert.Contains("API", strategies.Keys);
        }

        [Fact]
        public void Failover_GPS_StrategieCachePositions()
        {
            // Act
            var strategies = _service.ObtenirStrategiesFailover();

            // Assert
            Assert.Equal("Cache dernières positions connues", strategies["GPS"].StrategieSecours);
            Assert.Equal(TimeSpan.FromSeconds(3), strategies["GPS"].DelaiBasculement);
        }

        [Fact]
        public void Failover_BaseDonnees_StrategieReplica()
        {
            // Act
            var strategies = _service.ObtenirStrategiesFailover();

            // Assert
            Assert.Equal("Réplica lecture seule", strategies["BaseDonnees"].StrategieSecours);
            Assert.Equal(TimeSpan.FromSeconds(5), strategies["BaseDonnees"].DelaiBasculement);
        }

        [Fact]
        public void Failover_Notifications_StrategieFileLocale()
        {
            // Act
            var strategies = _service.ObtenirStrategiesFailover();

            // Assert
            Assert.Equal("File d'attente locale", strategies["Notifications"].StrategieSecours);
        }

        [Fact]
        public async Task Failover_CircuitOuvert_UtiliseSecours()
        {
            // Arrange
            _service.SimulerPanne("GPS");
            Func<Task<string>> principal = () => throw new Exception("GPS en panne");
            Func<Task<string>> secours = () => Task.FromResult("Position cache: 45.4215,-75.6972");

            // Act
            var resultat = await _service.ExecuterAvecResilience("GPS", principal, secours);

            // Assert
            Assert.True(resultat.Succes);
            Assert.True(resultat.UtiliseFailover);
            Assert.Contains("cache", resultat.Valeur);
        }

        [Fact]
        public async Task Failover_SansSecours_RetourneErreur()
        {
            // Arrange
            _service.SimulerPanne("GPS");
            Func<Task<string>> principal = () => throw new Exception("GPS en panne");

            // Act
            var resultat = await _service.ExecuterAvecResilience<string>("GPS", principal, null);

            // Assert
            Assert.False(resultat.Succes);
            Assert.False(resultat.UtiliseFailover);
            Assert.Contains("Circuit ouvert", resultat.MessageErreur);
        }

        [Fact]
        public void Failover_RapportSante_IndiqueFallbackActif()
        {
            // Arrange
            _service.SimulerPanne("GPS");

            // Act
            var rapport = _service.ObtenirRapportSante();
            var composantGPS = rapport.Composants.First(c => c.Nom == "GPS");

            // Assert
            Assert.True(composantGPS.EnFailover);
            Assert.Equal("Cache dernières positions connues", composantGPS.StrategieActive);
        }
    }

    public class GEO16_RetryTests
    {
        private readonly ResilienceService _service;

        public GEO16_RetryTests()
        {
            _service = new ResilienceService();
        }

        // --- RETRY + BACKOFF ---

        [Fact]
        public async Task Retry_EchecPuisSucces_RetourneSucces()
        {
            // Arrange
            int appels = 0;
            Func<Task<string>> operation = () =>
            {
                appels++;
                if (appels < 3)
                    throw new Exception("Erreur temporaire");
                return Task.FromResult("Succès après retry");
            };

            // Act
            var resultat = await _service.ExecuterAvecResilience("API", operation);

            // Assert
            Assert.True(resultat.Succes);
            Assert.Equal("Succès après retry", resultat.Valeur);
            Assert.Equal(3, resultat.TentativesEffectuees);
            Assert.False(resultat.UtiliseFailover);
        }

        [Fact]
        public async Task Retry_SuccesPremiereTentative_UnSeulAppel()
        {
            // Arrange
            int appels = 0;
            Func<Task<string>> operation = () =>
            {
                appels++;
                return Task.FromResult("OK immédiat");
            };

            // Act
            var resultat = await _service.ExecuterAvecResilience("Alertes", operation);

            // Assert
            Assert.True(resultat.Succes);
            Assert.Equal(1, resultat.TentativesEffectuees);
            Assert.Equal(1, appels);
        }

        [Fact]
        public async Task Retry_ToutesEchouent_CircuitOuvre()
        {
            // Arrange — on va forcer plus d'échecs que le seuil CB
            var service = new ResilienceService();
            int appels = 0;
            Func<Task<string>> operationEchoue = () =>
            {
                appels++;
                throw new Exception($"Échec #{appels}");
            };

            // Act — exécuter 2 fois (3 retry × 2 = 6 échecs ≥ seuil 5)
            await service.ExecuterAvecResilience<string>("Notifications", operationEchoue);
            var resultat = await service.ExecuterAvecResilience<string>("Notifications", operationEchoue);

            // Assert
            var etat = service.ObtenirEtatCircuit("Notifications");
            Assert.Equal(EtatCircuitBreaker.Ouvert, etat.Etat);
        }
    }

    public class GEO16_RapportSanteTests
    {
        private readonly ResilienceService _service;

        public GEO16_RapportSanteTests()
        {
            _service = new ResilienceService();
        }

        // --- RAPPORT SANTÉ ---

        [Fact]
        public void RapportSante_ToutSain_100Pourcent()
        {
            // Act
            var rapport = _service.ObtenirRapportSante();

            // Assert
            Assert.Equal(NiveauSante.Sain, rapport.NiveauGlobal);
            Assert.Equal(6, rapport.ComposantsActifs);
            Assert.Equal(6, rapport.ComposantsTotal);
            Assert.Equal(100, rapport.DisponibilitePourcentage);
        }

        [Fact]
        public void RapportSante_Composants_DetailsComplets()
        {
            // Act
            var rapport = _service.ObtenirRapportSante();

            // Assert
            Assert.Equal(6, rapport.Composants.Count);
            foreach (var composant in rapport.Composants)
            {
                Assert.False(string.IsNullOrEmpty(composant.Nom));
                Assert.Equal(NiveauSante.Sain, composant.Niveau);
                Assert.Equal(EtatCircuitBreaker.Ferme, composant.EtatCB);
                Assert.False(composant.EnFailover);
            }
        }

        [Fact]
        public void RapportSante_DateRapport_Presente()
        {
            // Act
            var rapport = _service.ObtenirRapportSante();

            // Assert
            Assert.True(rapport.DateRapport > DateTime.MinValue);
            Assert.True(rapport.DateRapport <= DateTime.UtcNow);
        }
    }

    public class GEO16_EvenementsTests
    {
        private readonly ResilienceService _service;

        public GEO16_EvenementsTests()
        {
            _service = new ResilienceService();
        }

        // --- ÉVÉNEMENTS / TIMELINE ---

        [Fact]
        public void Evenements_InitialVide()
        {
            // Act
            var evenements = _service.ObtenirEvenements();

            // Assert
            Assert.Empty(evenements);
        }

        [Fact]
        public void Evenements_ApresPanne_EnregistreHistorique()
        {
            // Arrange & Act
            _service.SimulerPanne("GPS");
            var evenements = _service.ObtenirEvenements();

            // Assert — devrait avoir au moins : PanneDetectee, CircuitOuvert, FailoverActive
            Assert.True(evenements.Count >= 3);
            Assert.Contains(evenements, e => e.Type == "PanneDetectee" && e.Composant == "GPS");
            Assert.Contains(evenements, e => e.Type == "CircuitOuvert" && e.Composant == "GPS");
            Assert.Contains(evenements, e => e.Type == "FailoverActive" && e.Composant == "GPS");
        }

        [Fact]
        public void Evenements_ApresReset_EnregistreRecovery()
        {
            // Arrange
            _service.SimulerPanne("Notifications");

            // Act
            _service.ResetComposant("Notifications");
            var evenements = _service.ObtenirEvenements();

            // Assert
            Assert.Contains(evenements, e => e.Type == "ComposantReset" && e.Composant == "Notifications");
        }

        [Fact]
        public void Evenements_Limite_RespecteMax()
        {
            // Arrange — simuler plusieurs pannes
            _service.SimulerPanne("GPS");
            _service.SimulerPanne("BaseDonnees");
            _service.SimulerPanne("Notifications");

            // Act
            var evenements = _service.ObtenirEvenements(limite: 5);

            // Assert
            Assert.True(evenements.Count <= 5);
        }

        [Fact]
        public void Evenements_OrdreChronologiqueInverse()
        {
            // Arrange
            _service.SimulerPanne("GPS");
            _service.SimulerPanne("BaseDonnees");

            // Act
            var evenements = _service.ObtenirEvenements();

            // Assert — le plus récent en premier
            for (int i = 0; i < evenements.Count - 1; i++)
            {
                Assert.True(evenements[i].Timestamp >= evenements[i + 1].Timestamp);
            }
        }
    }
}
