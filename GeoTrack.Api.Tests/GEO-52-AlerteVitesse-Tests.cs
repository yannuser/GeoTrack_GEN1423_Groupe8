using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GeoTrack.Api.Tests
{
    // ============================================================
    // GEO-52 : Tests unitaires — AlerteVitesse
    // Projet  : GeoTrack (GEN1423 – Groupe 8)
    // Auteur  : Sory Fofana
    // Date    : 2026-08-05
    // Tickets : GEO-10 / GEO-52
    // ============================================================

    #region Modèles de test

    public enum SeveriteAlerte { Aucune, Avertissement, Alerte, Critique }
    public enum EtatSurveillance { Normal, EnObservation, Declenchee, Escaladee }

    public class ConfigurationSeuil
    {
        public double SeuilAvertissement { get; set; } = 55.0;
        public double SeuilAlerte        { get; set; } = 50.0;
        public double SeuilCritique      { get; set; } = 75.0;
        public int    EchantillonsRequis { get; set; } = 3;
        public int    CooldownMinutes    { get; set; } = 5;
        public int    MaxAlertesHeure    { get; set; } = 10;
        public int    MaxAlertesJour     { get; set; } = 50;
        public double ToleranceGps       { get; set; } = 2.0;
    }

    public class ResultatEvaluation
    {
        public SeveriteAlerte Severite      { get; set; }
        public bool           AlerteEnvoyee { get; set; }
        public string         Raison        { get; set; } = string.Empty;
        public EtatSurveillance NouvelEtat  { get; set; }
    }

    public class ContexteSurveillance
    {
        public EtatSurveillance Etat              { get; set; } = EtatSurveillance.Normal;
        public int              EchantillonsAlerte { get; set; } = 0;
        public DateTime?        DerniereAlerte     { get; set; }
        public int              AlertesHeure       { get; set; } = 0;
        public int              AlertesJour        { get; set; } = 0;
    }

    public interface INotificationService
    {
        void EnvoyerPush(string message, SeveriteAlerte severite);
        void EnvoyerSms(string message);
        void EnvoyerEmail(string destinataire, string message);
        void EnvoyerDashboard(string message, SeveriteAlerte severite);
    }

    /// <summary>
    /// Stub manuel de INotificationService : enregistre chaque appel pour
    /// permettre de compter les invocations (remplace Mock/Verify).
    /// </summary>
    public class StubNotificationService : INotificationService
    {
        public List<(string Message, SeveriteAlerte Severite)> AppelsPush { get; } = new();
        public List<string> AppelsSms { get; } = new();
        public List<(string Destinataire, string Message)> AppelsEmail { get; } = new();
        public List<(string Message, SeveriteAlerte Severite)> AppelsDashboard { get; } = new();

        // Nombre total d'appels, toutes sévérités confondues
        public int NbPush      => AppelsPush.Count;
        public int NbSms       => AppelsSms.Count;
        public int NbEmail     => AppelsEmail.Count;
        public int NbDashboard => AppelsDashboard.Count;

        // Nombre d'appels pour une sévérité donnée
        public int NbPushPour(SeveriteAlerte severite)
            => AppelsPush.Count(a => a.Severite == severite);

        public int NbDashboardPour(SeveriteAlerte severite)
            => AppelsDashboard.Count(a => a.Severite == severite);

        public void EnvoyerPush(string message, SeveriteAlerte severite)
            => AppelsPush.Add((message, severite));

        public void EnvoyerSms(string message)
            => AppelsSms.Add(message);

        public void EnvoyerEmail(string destinataire, string message)
            => AppelsEmail.Add((destinataire, message));

        public void EnvoyerDashboard(string message, SeveriteAlerte severite)
            => AppelsDashboard.Add((message, severite));
    }

    public class AlerteVitesseService
    {
        private readonly ConfigurationSeuil    _config;
        private readonly INotificationService  _notifications;
        private          ContexteSurveillance  _contexte = new();

        public AlerteVitesseService(ConfigurationSeuil config, INotificationService notifications)
        {
            _config        = config;
            _notifications = notifications;
        }

        public EtatSurveillance EtatCourant => _contexte.Etat;

        public ResultatEvaluation EvaluerVitesse(double vitesseKmh)
        {
            // Filtre bruit GPS
            double vitesseEffective = vitesseKmh - _config.ToleranceGps;

            // Vitesse normale
            if (vitesseEffective <= _config.SeuilAlerte)
            {
                _contexte.EchantillonsAlerte = 0;
                _contexte.Etat = EtatSurveillance.Normal;
                return new ResultatEvaluation
                {
                    Severite      = SeveriteAlerte.Aucune,
                    AlerteEnvoyee = false,
                    Raison        = "Vitesse normale",
                    NouvelEtat    = EtatSurveillance.Normal
                };
            }

            // Critique immédiat
            if (vitesseEffective >= _config.SeuilCritique)
            {
                if (VerifierAntiSpam())
                {
                    EnregistrerAlerte();
                    _notifications.EnvoyerPush($"CRITIQUE : {vitesseKmh} km/h", SeveriteAlerte.Critique);
                    _notifications.EnvoyerSms($"Alerte critique vitesse : {vitesseKmh} km/h");
                    _notifications.EnvoyerEmail("admin@geotrack.ca", $"Vitesse critique : {vitesseKmh} km/h");
                    _contexte.Etat = EtatSurveillance.Escaladee;
                    return new ResultatEvaluation
                    {
                        Severite      = SeveriteAlerte.Critique,
                        AlerteEnvoyee = true,
                        Raison        = "Vitesse critique",
                        NouvelEtat    = EtatSurveillance.Escaladee
                    };
                }
                return ResultatAntiSpam();
            }

            // Avertissement — accumulation échantillons
            if (vitesseEffective >= _config.SeuilAvertissement)
            {
                _contexte.EchantillonsAlerte++;
                _contexte.Etat = EtatSurveillance.EnObservation;

                if (_contexte.EchantillonsAlerte >= _config.EchantillonsRequis)
                {
                    if (VerifierAntiSpam())
                    {
                        EnregistrerAlerte();
                        _notifications.EnvoyerPush($"ALERTE : {vitesseKmh} km/h", SeveriteAlerte.Alerte);
                        _notifications.EnvoyerDashboard($"Alerte vitesse : {vitesseKmh} km/h", SeveriteAlerte.Alerte);
                        _contexte.Etat = EtatSurveillance.Declenchee;
                        return new ResultatEvaluation
                        {
                            Severite      = SeveriteAlerte.Alerte,
                            AlerteEnvoyee = true,
                            Raison        = "Alerte confirmée après échantillons",
                            NouvelEtat    = EtatSurveillance.Declenchee
                        };
                    }
                    return ResultatAntiSpam();
                }

                return new ResultatEvaluation
                {
                    Severite      = SeveriteAlerte.Avertissement,
                    AlerteEnvoyee = false,
                    Raison        = $"En observation ({_contexte.EchantillonsAlerte}/{_config.EchantillonsRequis})",
                    NouvelEtat    = EtatSurveillance.EnObservation
                };
            }

            return new ResultatEvaluation
            {
                Severite      = SeveriteAlerte.Aucune,
                AlerteEnvoyee = false,
                Raison        = "Sous le seuil d'avertissement",
                NouvelEtat    = _contexte.Etat
            };
        }

        public bool VerifierAntiSpam()
        {
            if (_contexte.DerniereAlerte.HasValue)
            {
                var elapsed = DateTime.UtcNow - _contexte.DerniereAlerte.Value;
                if (elapsed.TotalMinutes < _config.CooldownMinutes) return false;
            }
            if (_contexte.AlertesHeure >= _config.MaxAlertesHeure) return false;
            if (_contexte.AlertesJour  >= _config.MaxAlertesJour)  return false;
            return true;
        }

        public void ResetContexte()
        {
            _contexte = new ContexteSurveillance();
        }

        public void SimulerDerniereAlerte(DateTime quand)
        {
            _contexte.DerniereAlerte = quand;
        }

        public void SimulerQuotaHeure(int nb)  => _contexte.AlertesHeure = nb;
        public void SimulerQuotaJour(int nb)   => _contexte.AlertesJour  = nb;

        private void EnregistrerAlerte()
        {
            _contexte.DerniereAlerte = DateTime.UtcNow;
            _contexte.AlertesHeure++;
            _contexte.AlertesJour++;
            _contexte.EchantillonsAlerte = 0;
        }

        private ResultatEvaluation ResultatAntiSpam() => new()
        {
            Severite      = SeveriteAlerte.Aucune,
            AlerteEnvoyee = false,
            Raison        = "Anti-spam actif",
            NouvelEtat    = _contexte.Etat
        };
    }

    #endregion

    // ============================================================
    // 1. Tests AlerteVitesseService (8 tests)
    // ============================================================
    public class AlerteVitesseServiceTests
    {
        private readonly ConfigurationSeuil   _config;
        private readonly StubNotificationService _notif;
        private readonly AlerteVitesseService _service;

        public AlerteVitesseServiceTests()
        {
            _config = new ConfigurationSeuil
            {
                SeuilAvertissement = 55.0,
                SeuilAlerte        = 50.0,
                SeuilCritique      = 75.0,
                EchantillonsRequis = 3,
                CooldownMinutes    = 5,
                MaxAlertesHeure    = 10,
                MaxAlertesJour     = 50,
                ToleranceGps       = 2.0
            };
            _notif   = new StubNotificationService();
            _service = new AlerteVitesseService(_config, _notif);
        }

        [Fact]
        public void EvaluerVitesse_VitesseNormale_AucuneAlerte()
        {
            // Arrange : 45 km/h — sous le seuil
            // Act
            var resultat = _service.EvaluerVitesse(45.0);

            // Assert
            Assert.Equal(SeveriteAlerte.Aucune, resultat.Severite);
            Assert.False(resultat.AlerteEnvoyee);
            Assert.Equal(EtatSurveillance.Normal, resultat.NouvelEtat);
            Assert.Equal(0, _notif.NbPush);
        }

        [Fact]
        public void EvaluerVitesse_DepassementAvertissement_MiseEnObservation()
        {
            // Arrange : 58 km/h — dépasse seuil avertissement (55) mais < 3 échantillons
            // Act
            var resultat = _service.EvaluerVitesse(58.0);

            // Assert
            Assert.Equal(SeveriteAlerte.Avertissement, resultat.Severite);
            Assert.False(resultat.AlerteEnvoyee);
            Assert.Equal(EtatSurveillance.EnObservation, resultat.NouvelEtat);
        }

        [Fact]
        public void EvaluerVitesse_TroisEchantillonsConsecutifs_AlerteConfirmee()
        {
            // Arrange : 3 mesures à 62 km/h
            _service.EvaluerVitesse(62.0);
            _service.EvaluerVitesse(62.0);

            // Act — 3e échantillon déclenche l'alerte
            var resultat = _service.EvaluerVitesse(62.0);

            // Assert
            Assert.Equal(SeveriteAlerte.Alerte, resultat.Severite);
            Assert.True(resultat.AlerteEnvoyee);
            Assert.Equal(EtatSurveillance.Declenchee, resultat.NouvelEtat);
            Assert.Equal(1, _notif.NbPushPour(SeveriteAlerte.Alerte));
        }

        [Fact]
        public void EvaluerVitesse_VitesseCritique_AlerteImmediateEtEscalade()
        {
            // Arrange : 95 km/h — critique immédiat
            // Act
            var resultat = _service.EvaluerVitesse(95.0);

            // Assert
            Assert.Equal(SeveriteAlerte.Critique, resultat.Severite);
            Assert.True(resultat.AlerteEnvoyee);
            Assert.Equal(EtatSurveillance.Escaladee, resultat.NouvelEtat);
            Assert.Equal(1, _notif.NbPushPour(SeveriteAlerte.Critique));
            Assert.Equal(1, _notif.NbSms);
            Assert.Equal(1, _notif.NbEmail);
        }

        [Fact]
        public void EvaluerVitesse_RetourNormale_ReinitialisationEtat()
        {
            // Arrange : alerte déclenchée puis retour normal
            _service.EvaluerVitesse(62.0);
            _service.EvaluerVitesse(62.0);
            _service.EvaluerVitesse(62.0);

            // Act
            _service.ResetContexte();
            var resultat = _service.EvaluerVitesse(45.0);

            // Assert
            Assert.Equal(SeveriteAlerte.Aucune, resultat.Severite);
            Assert.Equal(EtatSurveillance.Normal, resultat.NouvelEtat);
        }

        [Fact]
        public void EvaluerVitesse_ToleranceGps_VitesseEffectiveCorrigee()
        {
            // Arrange : 52 km/h - 2 km/h tolérance = 50 km/h effectif (= seuil, pas au-dessus)
            var resultat = _service.EvaluerVitesse(52.0);

            // Assert : 52 - 2 = 50 = SeuilAlerte → pas d'alerte
            Assert.Equal(SeveriteAlerte.Aucune, resultat.Severite);
        }

        [Fact]
        public void EvaluerVitesse_EtatInitial_EstNormal()
        {
            Assert.Equal(EtatSurveillance.Normal, _service.EtatCourant);
        }

        [Fact]
        public void EvaluerVitesse_VitesseLimite_SousSeuilCritique_PasEscalade()
        {
            // Arrange : 76 km/h - 2 tolérance = 74 km/h < 75 critique
            var resultat = _service.EvaluerVitesse(76.0);

            // Assert : pas critique (74 < 75), mais avertissement
            Assert.NotEqual(SeveriteAlerte.Critique, resultat.Severite);
        }
    }

    // ============================================================
    // 2. Tests Anti-Spam (6 tests)
    // ============================================================
    public class AntiSpamTests
    {
        private readonly ConfigurationSeuil        _config;
        private readonly StubNotificationService   _notif;
        private readonly AlerteVitesseService      _service;

        public AntiSpamTests()
        {
            _config = new ConfigurationSeuil
            {
                SeuilCritique   = 75.0,
                CooldownMinutes = 5,
                MaxAlertesHeure = 10,
                MaxAlertesJour  = 50,
                ToleranceGps    = 0.0
            };
            _notif   = new StubNotificationService();
            _service = new AlerteVitesseService(_config, _notif);
        }

        [Fact]
        public void AntiSpam_CooldownActif_AlerteBloquee()
        {
            // Arrange : dernière alerte il y a 2 min (< 5 min cooldown)
            _service.SimulerDerniereAlerte(DateTime.UtcNow.AddMinutes(-2));

            // Act
            var bloque = !_service.VerifierAntiSpam();

            // Assert
            Assert.True(bloque);
        }

        [Fact]
        public void AntiSpam_CooldownExpire_AlerteAutorisee()
        {
            // Arrange : dernière alerte il y a 6 min (> 5 min cooldown)
            _service.SimulerDerniereAlerte(DateTime.UtcNow.AddMinutes(-6));

            // Act
            var autorise = _service.VerifierAntiSpam();

            // Assert
            Assert.True(autorise);
        }

        [Fact]
        public void AntiSpam_QuotaHeureAtteint_AlerteBloquee()
        {
            // Arrange : 10 alertes cette heure (= max)
            _service.SimulerQuotaHeure(10);

            // Act
            var bloque = !_service.VerifierAntiSpam();

            // Assert
            Assert.True(bloque);
        }

        [Fact]
        public void AntiSpam_QuotaJourAtteint_AlerteBloquee()
        {
            // Arrange : 50 alertes aujourd'hui (= max)
            _service.SimulerQuotaJour(50);

            // Act
            var bloque = !_service.VerifierAntiSpam();

            // Assert
            Assert.True(bloque);
        }

        [Fact]
        public void AntiSpam_Reset_QuotasRemisAZero()
        {
            // Arrange : quotas pleins
            _service.SimulerQuotaHeure(10);
            _service.SimulerQuotaJour(50);

            // Act
            _service.ResetContexte();

            // Assert
            Assert.True(_service.VerifierAntiSpam());
        }

        [Fact]
        public void AntiSpam_PremierAlerte_AucunCooldown_Autorisee()
        {
            // Arrange : aucune alerte précédente
            // Act
            var autorise = _service.VerifierAntiSpam();

            // Assert
            Assert.True(autorise);
        }
    }

    // ============================================================
    // 3. Tests ConfigurationSeuil (4 tests)
    // ============================================================
    public class ConfigurationSeuilTests
    {
        [Fact]
        public void ConfigurationSeuil_ValeursParDefaut_SontValides()
        {
            var config = new ConfigurationSeuil();

            Assert.True(config.SeuilAlerte < config.SeuilAvertissement,
                "SeuilAlerte doit etre inferieur a SeuilAvertissement");
            Assert.True(config.SeuilAvertissement < config.SeuilCritique,
                "SeuilAvertissement doit etre inferieur a SeuilCritique");
            Assert.True(config.EchantillonsRequis > 0,
                "EchantillonsRequis doit etre strictement positif");
            Assert.True(config.CooldownMinutes > 0,
                "CooldownMinutes doit etre strictement positif");
        }

        [Fact]
        public void ConfigurationSeuil_SeuilAlerte_InferieurAvertissement()
        {
            var config = new ConfigurationSeuil
            {
                SeuilAlerte        = 50.0,
                SeuilAvertissement = 55.0
            };

            Assert.True(config.SeuilAlerte < config.SeuilAvertissement,
                "SeuilAlerte doit etre inferieur a SeuilAvertissement");
        }

        [Fact]
        public void ConfigurationSeuil_SeuilCritique_SuperieurAvertissement()
        {
            var config = new ConfigurationSeuil
            {
                SeuilAvertissement = 55.0,
                SeuilCritique      = 75.0
            };

            Assert.True(config.SeuilCritique > config.SeuilAvertissement,
                "SeuilCritique doit etre superieur a SeuilAvertissement");
        }

        [Fact]
        public void ConfigurationSeuil_MaxAlertes_BornesPositives()
        {
            var config = new ConfigurationSeuil();

            Assert.True(config.MaxAlertesHeure > 0,
                "MaxAlertesHeure doit etre strictement positif");
            Assert.True(config.MaxAlertesJour > config.MaxAlertesHeure,
                "MaxAlertesJour doit etre superieur a MaxAlertesHeure");
        }
    }

    // ============================================================
    // 4. Tests Notifications (4 tests)
    // ============================================================
    public class NotificationTests
    {
        private readonly StubNotificationService    _notif;
        private readonly AlerteVitesseService       _service;

        public NotificationTests()
        {
            var config = new ConfigurationSeuil
            {
                SeuilAvertissement = 55.0,
                SeuilAlerte        = 50.0,
                SeuilCritique      = 75.0,
                EchantillonsRequis = 1,
                CooldownMinutes    = 5,
                MaxAlertesHeure    = 100,
                MaxAlertesJour     = 1000,
                ToleranceGps       = 0.0
            };
            _notif   = new StubNotificationService();
            _service = new AlerteVitesseService(config, _notif);
        }

        [Fact]
        public void Notification_AlerteCritique_EnvoiPushSmsMail()
        {
            _service.EvaluerVitesse(80.0);

            Assert.Equal(1, _notif.NbPushPour(SeveriteAlerte.Critique));
            Assert.Equal(1, _notif.NbSms);
            Assert.Equal(1, _notif.NbEmail);
        }

        [Fact]
        public void Notification_AlerteNormale_EnvoiPushEtDashboard()
        {
            _service.EvaluerVitesse(60.0);

            Assert.Equal(1, _notif.NbPushPour(SeveriteAlerte.Alerte));
            Assert.Equal(1, _notif.NbDashboardPour(SeveriteAlerte.Alerte));
            Assert.Equal(0, _notif.NbSms);
        }

        [Fact]
        public void Notification_VitesseNormale_AucuneNotification()
        {
            _service.EvaluerVitesse(40.0);

            Assert.Equal(0, _notif.NbPush);
            Assert.Equal(0, _notif.NbSms);
            Assert.Equal(0, _notif.NbEmail);
        }

        [Fact]
        public void Notification_AntiSpamActif_AucuneNotification()
        {
            // Première alerte passe
            _service.EvaluerVitesse(80.0);
            // Simule cooldown actif
            _service.SimulerDerniereAlerte(DateTime.UtcNow.AddMinutes(-1));

            // Deuxième alerte bloquée
            _service.EvaluerVitesse(80.0);

            Assert.Equal(1, _notif.NbPush);
        }
    }
}
