// =============================================================================
// GEO-51 : Service AlerteVitesse — GeoTrack GEN1423 Groupe 8
// Auteur  : Sory Fofana
// Date    : 2026-08-05
// Story   : GEO-10 — Alerte de dépassement de vitesse
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeoTrack.Api.Services
{
    // =========================================================================
    // ENUMS
    // =========================================================================

    public enum SeveriteAlerte
    {
        Aucune = 0,
        Avertissement = 1,
        Alerte = 2,
        Critique = 3
    }

    public enum EtatSurveillance
    {
        Normal = 0,
        EnObservation = 1,
        Declenchee = 2,
        Escaladee = 3
    }

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    public class ConfigurationSeuil
    {
        // Seuils de vitesse (km/h)
        public double SeuilAvertissement { get; set; } = 55.0;
        public double SeuilAlerte { get; set; } = 60.0;
        public double SeuilCritique { get; set; } = 75.0;

        // Tolérance GPS (km/h) — filtre bruit
        public double ToleranceGps { get; set; } = 3.0;

        // Nombre d'échantillons consécutifs requis pour confirmer
        public int EchantillonsRequis { get; set; } = 3;

        // Durée minimum de dépassement (secondes)
        public int DureeMinimumSecondes { get; set; } = 5;

        // Anti-spam
        public int CooldownMinutes { get; set; } = 5;
        public int MaxAlertesParHeure { get; set; } = 10;
        public int MaxAlertesParJour { get; set; } = 50;
        public int SeuilEscalade { get; set; } = 3;
    }

    // =========================================================================
    // DTOs
    // =========================================================================

    public class DonneeVitesse
    {
        public string AppareilId { get; set; }
        public double Vitesse { get; set; }          // km/h
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Horodatage { get; set; }
    }

    public class ResultatEvaluation
    {
        public SeveriteAlerte Severite { get; set; }
        public EtatSurveillance Etat { get; set; }
        public bool AlerteEnvoyee { get; set; }
        public string Raison { get; set; }
        public double VitesseMesuree { get; set; }
        public double SeuilDepasse { get; set; }
        public DateTime Horodatage { get; set; }
    }

    public class ContexteSurveillance
    {
        public string AppareilId { get; set; }
        public EtatSurveillance Etat { get; set; } = EtatSurveillance.Normal;
        public int EchantillonsConsecutifs { get; set; } = 0;
        public DateTime? DebutDepassement { get; set; }
        public DateTime? DerniereAlerte { get; set; }
        public int AlertesHeure { get; set; } = 0;
        public int AlertesJour { get; set; } = 0;
        public int AlertesConsecutives { get; set; } = 0;
        public DateTime FenetreHeure { get; set; } = DateTime.UtcNow;
        public DateTime FenetreJour { get; set; } = DateTime.UtcNow.Date;
    }

    // =========================================================================
    // INTERFACE NOTIFICATION
    // =========================================================================

    public interface INotificationService
    {
        Task EnvoyerPush(string appareilId, string message, SeveriteAlerte severite);
        Task EnvoyerSms(string appareilId, string message);
        Task EnvoyerEmail(string appareilId, string message);
        Task EnvoyerDashboard(string appareilId, ResultatEvaluation resultat);
    }

    // =========================================================================
    // SERVICE PRINCIPAL
    // =========================================================================

    public class AlerteVitesseService
    {
        private readonly ConfigurationSeuil _config;
        private readonly INotificationService _notifications;
        private readonly Dictionary<string, ContexteSurveillance> _contextes;

        public AlerteVitesseService(
            ConfigurationSeuil config,
            INotificationService notifications)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _contextes = new Dictionary<string, ContexteSurveillance>();
        }

        // ---------------------------------------------------------------------
        // MÉTHODE PRINCIPALE : Évaluer une mesure de vitesse
        // ---------------------------------------------------------------------
        public async Task<ResultatEvaluation> EvaluerVitesse(DonneeVitesse donnee)
        {
            if (donnee == null)
                throw new ArgumentNullException(nameof(donnee));

            // Récupérer ou créer le contexte de l'appareil
            var contexte = ObtenirContexte(donnee.AppareilId);

            // Réinitialiser les fenêtres temporelles si nécessaire
            ReinitialiserFenetres(contexte);

            // Appliquer la tolérance GPS (filtre bruit)
            double vitesseCorrigee = Math.Max(0, donnee.Vitesse - _config.ToleranceGps);

            // Déterminer la sévérité brute
            SeveriteAlerte severite = DeterminerSeverite(vitesseCorrigee);

            // Mettre à jour l'état de surveillance
            MettreAJourEtat(contexte, severite, donnee.Horodatage);

            // Construire le résultat
            var resultat = new ResultatEvaluation
            {
                Severite = severite,
                Etat = contexte.Etat,
                VitesseMesuree = donnee.Vitesse,
                SeuilDepasse = ObtenirSeuil(severite),
                Horodatage = donnee.Horodatage,
                AlerteEnvoyee = false,
                Raison = "Vitesse normale"
            };

            // Déclencher une alerte si nécessaire
            if (DoitDeclencher(contexte, severite))
            {
                bool antiSpamOk = VerifierAntiSpam(contexte);

                if (antiSpamOk)
                {
                    await DeclencherAlerte(donnee, severite, contexte, resultat);
                }
                else
                {
                    resultat.Raison = "Alerte bloquée par anti-spam";
                    resultat.AlerteEnvoyee = false;
                }
            }
            else if (severite == SeveriteAlerte.Aucune && contexte.Etat != EtatSurveillance.Normal)
            {
                // Retour à la normale
                ReinitialiserEtat(contexte);
                resultat.Raison = "Retour à la normale";
            }

            return resultat;
        }

        // ---------------------------------------------------------------------
        // Déterminer la sévérité selon la vitesse
        // ---------------------------------------------------------------------
        private SeveriteAlerte DeterminerSeverite(double vitesse)
        {
            if (vitesse >= _config.SeuilCritique)
                return SeveriteAlerte.Critique;
            if (vitesse >= _config.SeuilAlerte)
                return SeveriteAlerte.Alerte;
            if (vitesse >= _config.SeuilAvertissement)
                return SeveriteAlerte.Avertissement;
            return SeveriteAlerte.Aucune;
        }

        // ---------------------------------------------------------------------
        // Mettre à jour la machine à états
        // ---------------------------------------------------------------------
        private void MettreAJourEtat(
            ContexteSurveillance contexte,
            SeveriteAlerte severite,
            DateTime horodatage)
        {
            if (severite == SeveriteAlerte.Aucune)
            {
                contexte.EchantillonsConsecutifs = 0;
                contexte.DebutDepassement = null;
                return;
            }

            // Critique → déclenchement immédiat sans attendre les échantillons
            if (severite == SeveriteAlerte.Critique)
            {
                contexte.Etat = EtatSurveillance.Declenchee;
                contexte.EchantillonsConsecutifs = _config.EchantillonsRequis;
                if (contexte.DebutDepassement == null)
                    contexte.DebutDepassement = horodatage;
                return;
            }

            // Incrémenter les échantillons consécutifs
            contexte.EchantillonsConsecutifs++;

            if (contexte.DebutDepassement == null)
                contexte.DebutDepassement = horodatage;

            // Transition Normal → EnObservation
            if (contexte.Etat == EtatSurveillance.Normal &&
                contexte.EchantillonsConsecutifs >= 1)
            {
                contexte.Etat = EtatSurveillance.EnObservation;
            }

            // Transition EnObservation → Declenchee
            if (contexte.Etat == EtatSurveillance.EnObservation &&
                contexte.EchantillonsConsecutifs >= _config.EchantillonsRequis)
            {
                double duree = (horodatage - contexte.DebutDepassement.Value).TotalSeconds;
                if (duree >= _config.DureeMinimumSecondes)
                    contexte.Etat = EtatSurveillance.Declenchee;
            }
        }

        // ---------------------------------------------------------------------
        // Vérifier si on doit déclencher une alerte
        // ---------------------------------------------------------------------
        private bool DoitDeclencher(ContexteSurveillance contexte, SeveriteAlerte severite)
        {
            if (severite == SeveriteAlerte.Aucune)
                return false;

            // Critique → toujours déclencher
            if (severite == SeveriteAlerte.Critique)
                return true;

            // Déclencher seulement si l'état est Declenchee ou Escaladee
            return contexte.Etat == EtatSurveillance.Declenchee ||
                   contexte.Etat == EtatSurveillance.Escaladee;
        }

        // ---------------------------------------------------------------------
        // Vérifier les règles anti-spam
        // ---------------------------------------------------------------------
        private bool VerifierAntiSpam(ContexteSurveillance contexte)
        {
            DateTime maintenant = DateTime.UtcNow;

            // Vérifier cooldown
            if (contexte.DerniereAlerte.HasValue)
            {
                double minutesDepuis = (maintenant - contexte.DerniereAlerte.Value).TotalMinutes;
                if (minutesDepuis < _config.CooldownMinutes)
                    return false;
            }

            // Vérifier quota horaire
            if (contexte.AlertesHeure >= _config.MaxAlertesParHeure)
                return false;

            // Vérifier quota journalier
            if (contexte.AlertesJour >= _config.MaxAlertesParJour)
                return false;

            return true;
        }

        // ---------------------------------------------------------------------
        // Déclencher l'alerte et envoyer les notifications
        // ---------------------------------------------------------------------
        private async Task DeclencherAlerte(
            DonneeVitesse donnee,
            SeveriteAlerte severite,
            ContexteSurveillance contexte,
            ResultatEvaluation resultat)
        {
            // Vérifier escalade
            if (contexte.AlertesConsecutives >= _config.SeuilEscalade)
                contexte.Etat = EtatSurveillance.Escaladee;

            string message = FormaterMessage(donnee, severite);

            // Envoyer notifications selon sévérité
            switch (severite)
            {
                case SeveriteAlerte.Avertissement:
                    await _notifications.EnvoyerPush(donnee.AppareilId, message, severite);
                    await _notifications.EnvoyerDashboard(donnee.AppareilId, resultat);
                    break;

                case SeveriteAlerte.Alerte:
                    await _notifications.EnvoyerPush(donnee.AppareilId, message, severite);
                    await _notifications.EnvoyerEmail(donnee.AppareilId, message);
                    await _notifications.EnvoyerDashboard(donnee.AppareilId, resultat);
                    break;

                case SeveriteAlerte.Critique:
                    await _notifications.EnvoyerPush(donnee.AppareilId, message, severite);
                    await _notifications.EnvoyerSms(donnee.AppareilId, message);
                    await _notifications.EnvoyerEmail(donnee.AppareilId, message);
                    await _notifications.EnvoyerDashboard(donnee.AppareilId, resultat);
                    break;
            }

            // Mettre à jour les compteurs anti-spam
            contexte.DerniereAlerte = DateTime.UtcNow;
            contexte.AlertesHeure++;
            contexte.AlertesJour++;
            contexte.AlertesConsecutives++;

            resultat.AlerteEnvoyee = true;
            resultat.Raison = $"Alerte {severite} déclenchée — {donnee.Vitesse} km/h";
        }

        // ---------------------------------------------------------------------
        // Formater le message d'alerte
        // ---------------------------------------------------------------------
        private string FormaterMessage(DonneeVitesse donnee, SeveriteAlerte severite)
        {
            return severite switch
            {
                SeveriteAlerte.Critique =>
                    $"🚨 CRITIQUE — Appareil {donnee.AppareilId} : {donnee.Vitesse} km/h " +
                    $"(seuil {_config.SeuilCritique} km/h) à {donnee.Horodatage:HH:mm:ss}",
                SeveriteAlerte.Alerte =>
                    $"⚠️ ALERTE — Appareil {donnee.AppareilId} : {donnee.Vitesse} km/h " +
                    $"(seuil {_config.SeuilAlerte} km/h) à {donnee.Horodatage:HH:mm:ss}",
                SeveriteAlerte.Avertissement =>
                    $"ℹ️ AVERTISSEMENT — Appareil {donnee.AppareilId} : {donnee.Vitesse} km/h " +
                    $"(seuil {_config.SeuilAvertissement} km/h) à {donnee.Horodatage:HH:mm:ss}",
                _ => $"Vitesse normale : {donnee.Vitesse} km/h"
            };
        }

        // ---------------------------------------------------------------------
        // Obtenir le seuil correspondant à une sévérité
        // ---------------------------------------------------------------------
        private double ObtenirSeuil(SeveriteAlerte severite)
        {
            return severite switch
            {
                SeveriteAlerte.Critique => _config.SeuilCritique,
                SeveriteAlerte.Alerte => _config.SeuilAlerte,
                SeveriteAlerte.Avertissement => _config.SeuilAvertissement,
                _ => 0
            };
        }

        // ---------------------------------------------------------------------
        // Obtenir ou créer le contexte d'un appareil
        // ---------------------------------------------------------------------
        private ContexteSurveillance ObtenirContexte(string appareilId)
        {
            if (!_contextes.ContainsKey(appareilId))
                _contextes[appareilId] = new ContexteSurveillance { AppareilId = appareilId };
            return _contextes[appareilId];
        }

        // ---------------------------------------------------------------------
        // Réinitialiser les fenêtres temporelles (heure/jour)
        // ---------------------------------------------------------------------
        private void ReinitialiserFenetres(ContexteSurveillance contexte)
        {
            DateTime maintenant = DateTime.UtcNow;

            if ((maintenant - contexte.FenetreHeure).TotalHours >= 1)
            {
                contexte.AlertesHeure = 0;
                contexte.FenetreHeure = maintenant;
            }

            if (maintenant.Date > contexte.FenetreJour.Date)
            {
                contexte.AlertesJour = 0;
                contexte.AlertesConsecutives = 0;
                contexte.FenetreJour = maintenant.Date;
            }
        }

        // ---------------------------------------------------------------------
        // Réinitialiser l'état après retour à la normale
        // ---------------------------------------------------------------------
        private void ReinitialiserEtat(ContexteSurveillance contexte)
        {
            contexte.Etat = EtatSurveillance.Normal;
            contexte.EchantillonsConsecutifs = 0;
            contexte.DebutDepassement = null;
            contexte.AlertesConsecutives = 0;
        }
    }
}
