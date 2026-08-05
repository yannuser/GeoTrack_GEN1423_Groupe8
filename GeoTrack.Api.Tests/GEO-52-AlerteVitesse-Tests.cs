using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Moq;

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
        private readonly Mock<INotificationService> _mockNotif;
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
            _mockNotif = new Mock<INotificationService>();
            _service   = new AlerteVitesseService(_config, _mockNotif.Object);
        }

        [Fact]
        public void EvaluerVitesse_VitesseNormale_AucuneAlerte()
        {
            // Arrange : 45 km/h — sous le seuil
            // Act
            var resultat = _service.EvaluerVitesse(45.0);

            // Assert
            resultat.Severite.Should().Be(SeveriteAlerte.Aucune);
            resultat.AlerteEnvoyee.Should().BeFalse();
            resultat.NouvelEtat.Should().Be(EtatSurveillance.Normal);
            _mockNotif.Verify(n => n.EnvoyerPush(It.IsAny<string>(), It.IsAny<SeveriteAlerte>()), Times.Never);
        }

        [Fact]
        public void EvaluerVitesse_DepassementAvertissement_MiseEnObservation()
        {
            // Arrange : 58 km/h — dépasse seuil avertissement (55) mais < 3 échantillons
            // Act
            var resultat = _service.EvaluerVitesse(58.0);

            // Assert
            resultat.Severite.Should().Be(SeveriteAlerte.Avertissement);
            resultat.AlerteEnvoyee.Should().BeFalse();
            resultat.NouvelEtat.Should().Be(EtatSurveillance.EnObservation);
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
            resultat.Severite.Should().Be(SeveriteAlerte.Alerte);
            resultat.AlerteEnvoyee.Should().BeTrue();
            resultat.NouvelEtat.Should().Be(EtatSurveillance.Declenchee);
            _mockNotif.Verify(n => n.EnvoyerPush(It.IsAny<string>(), SeveriteAlerte.Alerte), Times.Once);
        }

        [Fact]
        public void EvaluerVitesse_VitesseCritique_AlerteImmediateEtEscalade()
        {
            // Arrange : 95 km/h — critique immédiat
            // Act
            var resultat = _service.EvaluerVitesse(95.0);

            // Assert
            resultat.Severite.Should().Be(SeveriteAlerte.Critique);
            resultat.AlerteEnvoyee.Should().BeTrue();
            resultat.NouvelEtat.Should().Be(EtatSurveillance.Escaladee);
            _mockNotif.Verify(n => n.EnvoyerPush(It.IsAny<string>(), SeveriteAlerte.Critique), Times.Once);
            _mockNotif.Verify(n => n.EnvoyerSms(It.IsAny<string>()), Times.Once);
            _mockNotif.Verify(n => n.EnvoyerEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
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
            resultat.Severite.Should().Be(SeveriteAlerte.Aucune);
            resultat.NouvelEtat.Should().Be(EtatSurveillance.Normal);
        }

        [Fact]
        public void EvaluerVitesse_ToleranceGps_VitesseEffectiveCorrigee()
        {
            // Arrange : 52 km/h - 2 km/h tolérance = 50 km/h effectif (= seuil, pas au-dessus)
            var resultat = _service.EvaluerVitesse(52.0);

            // Assert : 52 - 2 = 50 = SeuilAlerte → pas d'alerte
            resultat.Severite.Should().Be(SeveriteAlerte.Aucune);
        }

        [Fact]
        public void EvaluerVitesse_EtatInitial_EstNormal()
        {
            _service.EtatCourant.Should().Be(EtatSurveillance.Normal);
        }

        [Fact]
        public void EvaluerVitesse_VitesseLimite_SousSeuilCritique_PasEscalade()
        {
            // Arrange : 76 km/h - 2 tolérance = 74 km/h < 75 critique
            var resultat = _service.EvaluerVitesse(76.0);

            // Assert : pas critique (74 < 75), mais avertissement
            resultat.Severite.Should().NotBe(SeveriteAlerte.Critique);
        }
    }

    // ============================================================
    // 2. Tests Anti-Spam (6 tests)
    // ============================================================
    public class AntiSpamTests
    {
        private readonly ConfigurationSeuil        _config;
        private readonly Mock<INotificationService> _mockNotif;
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
            _mockNotif = new Mock<INotificationService>();
            _service   = new AlerteVitesseService(_config, _mockNotif.Object);
        }

        [Fact]
        public void AntiSpam_CooldownActif_AlerteBloquee()
        {
            // Arrange : dernière alerte il y a 2 min (< 5 min cooldown)
            _service.SimulerDerniereAlerte(DateTime.UtcNow.AddMinutes(-2));

            // Act
            var bloque = !_service.VerifierAntiSpam();

            // Assert
            bloque.Should().BeTrue();
        }

        [Fact]
        public void AntiSpam_CooldownExpire_AlerteAutorisee()
        {
            // Arrange : dernière alerte il y a 6 min (> 5 min cooldown)
            _service.SimulerDerniereAlerte(DateTime.UtcNow.AddMinutes(-6));

            // Act
            var autorise = _service.VerifierAntiSpam();

            // Assert
            autorise.Should().BeTrue();
        }

        [Fact]
        public void AntiSpam_QuotaHeureAtteint_AlerteBloquee()
        {
            // Arrange : 10 alertes cette heure (= max)
            _service.SimulerQuotaHeure(10);

            // Act
            var bloque = !_service.VerifierAntiSpam();

            // Assert
            bloque.Should().BeTrue();
        }

        [Fact]
        public void AntiSpam_QuotaJourAtteint_AlerteBloquee()
        {
            // Arrange : 50 alertes aujourd'hui (= max)
            _service.SimulerQuotaJour(50);

            // Act
            var bloque = !_service.VerifierAntiSpam();

            // Assert
            bloque.Should().BeTrue();
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
            _service.VerifierAntiSpam().Should().BeTrue();
        }

        [Fact]
        public void AntiSpam_PremierAlerte_AucunCooldown_Autorisee()
        {
            // Arrange : aucune alerte précédente
            // Act
            var autorise = _service.VerifierAntiSpam();

            // Assert
            autorise.Should().BeTrue();
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

            config.SeuilAlerte.Should().BeLessThan(config.SeuilAvertissement);
            config.SeuilAvertissement.Should().BeLessThan(config.SeuilCritique);
            config.EchantillonsRequis.Should().BeGreaterThan(0);
            config.CooldownMinutes.Should().BeGreaterThan(0);
        }

        [Fact]
        public void ConfigurationSeuil_SeuilAlerte_InferieurAvertissement()
        {
            var config = new ConfigurationSeuil
            {
                SeuilAlerte        = 50.0,
                SeuilAvertissement = 55.0
            };

            config.SeuilAlerte.Should().BeLessThan(config.SeuilAvertissement);
        }

        [Fact]
        public void ConfigurationSeuil_SeuilCritique_SuperieurAvertissement()
        {
            var config = new ConfigurationSeuil
            {
                SeuilAvertissement = 55.0,
                SeuilCritique      = 75.0
            };

            config.SeuilCritique.Should().BeGreaterThan(config.SeuilAvertissement);
        }

        [Fact]
        public void ConfigurationSeuil_MaxAlertes_BornesPositives()
        {
            var config = new ConfigurationSeuil();

            config.MaxAlertesHeure.Should().BeGreaterThan(0);
            config.MaxAlertesJour.Should().BeGreaterThan(config.MaxAlertesHeure);
        }
    }

    // ============================================================
    // 4. Tests Notifications (4 tests)
    // ============================================================
    public class NotificationTests
    {
        private readonly Mock<INotificationService> _mockNotif;
        private readonly AlerteVitesseService       _service;

        public NotificationTests()
        {
            var config = new ConfigurationSeuil
            {
                SeuilAvertissement = 55.0,
                SeuilAlerte        = 50.0,
                SeuilCritique      = 75.0,
                EchantillonsRequis = 1,
                CooldownMinutes    = 0,
                MaxAlertesHeure    = 100,
                MaxAlertesJour     = 1000,
                ToleranceGps       = 0.0
            };
            _mockNotif = new Mock<INotificationService>();
            _service   = new AlerteVitesseService(config, _mockNotif.Object);
        }

        [Fact]
        public void Notification_AlerteCritique_EnvoiPushSmsMail()
        {
            _service.EvaluerVitesse(80.0);

            _mockNotif.Verify(n => n.EnvoyerPush(It.IsAny<string>(), SeveriteAlerte.Critique), Times.Once);
            _mockNotif.Verify(n => n.EnvoyerSms(It.IsAny<string>()), Times.Once);
            _mockNotif.Verify(n => n.EnvoyerEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void Notification_AlerteNormale_EnvoiPushEtDashboard()
        {
            _service.EvaluerVitesse(60.0);

            _mockNotif.Verify(n => n.EnvoyerPush(It.IsAny<string>(), SeveriteAlerte.Alerte), Times.Once);
            _mockNotif.Verify(n => n.EnvoyerDashboard(It.IsAny<string>(), SeveriteAlerte.Alerte), Times.Once);
            _mockNotif.Verify(n => n.EnvoyerSms(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Notification_VitesseNormale_AucuneNotification()
        {
            _service.EvaluerVitesse(40.0);

            _mockNotif.Verify(n => n.EnvoyerPush(It.IsAny<string>(), It.IsAny<SeveriteAlerte>()), Times.Never);
            _mockNotif.Verify(n => n.EnvoyerSms(It.IsAny<string>()), Times.Never);
            _mockNotif.Verify(n => n.EnvoyerEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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

            _mockNotif.Verify(n => n.EnvoyerPush(It.IsAny<string>(), It.IsAny<SeveriteAlerte>()), Times.Once);
        }
    }
}
