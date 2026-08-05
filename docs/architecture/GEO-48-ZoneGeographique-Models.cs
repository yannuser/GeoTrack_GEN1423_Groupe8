// ============================================================================
// GEO-48 : Modèle de données - Entités Zone Géographique
// Projet : GeoTrack (GEN1423 - Groupe 8)
// Emplacement : GeoTrack.Api/Models/
// Auteur : Sory Fofana
// Date : 2026-08-05
// ============================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoTrack.Api.Models
{
    // ========================================================================
    // ENTITÉ PRINCIPALE : ZoneGeographique
    // Représente une zone de geofencing (inclusion ou exclusion)
    // ========================================================================
    public class ZoneGeographique
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Nom { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Type de zone : "inclusion" (zone permise) ou "exclusion" (zone interdite)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string TypeZone { get; set; } = "inclusion";

        /// <summary>
        /// Forme géométrique : "polygone", "cercle", "rectangle"
        /// </summary>
        [Required]
        [StringLength(20)]
        public string FormeGeometrique { get; set; } = "polygone";

        /// <summary>
        /// Coordonnées JSON du polygone (array de {lat, lng})
        /// Format : [{"lat": 45.123, "lng": -75.456}, ...]
        /// </summary>
        [Required]
        [Column(TypeName = "text")]
        public string CoordonneesJson { get; set; } = "[]";

        /// <summary>
        /// Centre latitude (pour zones circulaires)
        /// </summary>
        public double? CentreLatitude { get; set; }

        /// <summary>
        /// Centre longitude (pour zones circulaires)
        /// </summary>
        public double? CentreLongitude { get; set; }

        /// <summary>
        /// Rayon en mètres (pour zones circulaires)
        /// </summary>
        [Range(1, 100000)]
        public double? RayonMetres { get; set; }

        /// <summary>
        /// Couleur d'affichage sur la carte (hex)
        /// </summary>
        [StringLength(7)]
        public string Couleur { get; set; } = "#3388ff";

        /// <summary>
        /// Opacité de remplissage (0.0 à 1.0)
        /// </summary>
        [Range(0.0, 1.0)]
        public double Opacite { get; set; } = 0.3;

        /// <summary>
        /// Zone active ou non
        /// </summary>
        public bool EstActive { get; set; } = true;

        /// <summary>
        /// Soft delete : zone supprimée logiquement
        /// </summary>
        public bool EstSupprimee { get; set; } = false;

        // ----- Métadonnées temporelles -----
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime? DateModification { get; set; }
        public DateTime? DateSuppression { get; set; }

        // ----- Relations -----
        [Required]
        public int UtilisateurId { get; set; }

        [ForeignKey("UtilisateurId")]
        public virtual Utilisateur? Utilisateur { get; set; }

        public virtual ICollection<RegleAlerte> ReglesAlerte { get; set; } = new List<RegleAlerte>();
        public virtual ICollection<HistoriqueEvenement> Evenements { get; set; } = new List<HistoriqueEvenement>();
    }

    // ========================================================================
    // ENTITÉ : RegleAlerte
    // Règles d'alerte associées à une zone (entrée, sortie, vitesse)
    // ========================================================================
    public class RegleAlerte
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ZoneGeographiqueId { get; set; }

        [ForeignKey("ZoneGeographiqueId")]
        public virtual ZoneGeographique? ZoneGeographique { get; set; }

        /// <summary>
        /// Type d'événement déclencheur : "entree", "sortie", "vitesse", "immobilite"
        /// </summary>
        [Required]
        [StringLength(30)]
        public string TypeEvenement { get; set; } = "sortie";

        /// <summary>
        /// Seuil de vitesse en km/h (pour alertes de type "vitesse")
        /// </summary>
        [Range(0, 300)]
        public int? SeuilVitesseKmH { get; set; }

        /// <summary>
        /// Durée d'immobilité en minutes (pour alertes de type "immobilite")
        /// </summary>
        [Range(1, 1440)]
        public int? DureeImmobiliteMinutes { get; set; }

        /// <summary>
        /// Niveau de sévérité : "info", "warning", "critical"
        /// </summary>
        [Required]
        [StringLength(20)]
        public string NiveauSeverite { get; set; } = "warning";

        /// <summary>
        /// Message personnalisé affiché lors de l'alerte
        /// </summary>
        [StringLength(300)]
        public string? MessageAlerte { get; set; }

        /// <summary>
        /// Canaux de notification (JSON array) : ["push", "email", "sms"]
        /// </summary>
        [StringLength(100)]
        public string CanauxNotification { get; set; } = "[\"push\"]";

        /// <summary>
        /// Délai anti-spam en secondes entre deux alertes consécutives
        /// </summary>
        [Range(0, 86400)]
        public int DelaiAntiSpamSecondes { get; set; } = 300;

        /// <summary>
        /// Plage horaire active - heure de début (null = toujours actif)
        /// </summary>
        public TimeSpan? HeureDebut { get; set; }

        /// <summary>
        /// Plage horaire active - heure de fin
        /// </summary>
        public TimeSpan? HeureFin { get; set; }

        public bool EstActive { get; set; } = true;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    }

    // ========================================================================
    // ENTITÉ : HistoriqueEvenement
    // Journal des événements de geofencing (entrées, sorties, alertes)
    // ========================================================================
    public class HistoriqueEvenement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ZoneGeographiqueId { get; set; }

        [ForeignKey("ZoneGeographiqueId")]
        public virtual ZoneGeographique? ZoneGeographique { get; set; }

        [Required]
        public int AppareilId { get; set; }

        [ForeignKey("AppareilId")]
        public virtual Appareil? Appareil { get; set; }

        /// <summary>
        /// Type d'événement : "entree", "sortie", "alerte_vitesse", "alerte_immobilite"
        /// </summary>
        [Required]
        [StringLength(30)]
        public string TypeEvenement { get; set; } = string.Empty;

        /// <summary>
        /// Position GPS au moment de l'événement
        /// </summary>
        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        /// <summary>
        /// Vitesse de l'appareil en km/h au moment de l'événement
        /// </summary>
        [Range(0, 500)]
        public double? VitesseKmH { get; set; }

        /// <summary>
        /// Alerte envoyée avec succès
        /// </summary>
        public bool AlerteEnvoyee { get; set; } = false;

        public DateTime DateEvenement { get; set; } = DateTime.UtcNow;
    }

    // ========================================================================
    // ENTITÉ : Appareil
    // Appareil GPS suivi (véhicule, personne, objet)
    // ========================================================================
    public class Appareil
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;

        /// <summary>
        /// Type d'appareil : "vehicule", "personne", "objet"
        /// </summary>
        [Required]
        [StringLength(20)]
        public string TypeAppareil { get; set; } = "vehicule";

        /// <summary>
        /// Identifiant unique de l'appareil (IMEI, MAC, etc.)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string IdentifiantUnique { get; set; } = string.Empty;

        /// <summary>
        /// Dernière position connue
        /// </summary>
        public double? DerniereLatitude { get; set; }
        public double? DerniereLongitude { get; set; }
        public DateTime? DerniereMiseAJour { get; set; }

        public bool EstActif { get; set; } = true;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        // ----- Relations -----
        [Required]
        public int UtilisateurId { get; set; }

        [ForeignKey("UtilisateurId")]
        public virtual Utilisateur? Utilisateur { get; set; }

        public virtual ICollection<HistoriqueEvenement> Evenements { get; set; } = new List<HistoriqueEvenement>();
    }

    // ========================================================================
    // ENTITÉ : Utilisateur
    // Utilisateur du système GeoTrack
    // ========================================================================
    public class Utilisateur
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Prenom { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string MotDePasseHash { get; set; } = string.Empty;

        /// <summary>
        /// Rôle : "admin", "gestionnaire", "utilisateur"
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "utilisateur";

        public bool EstActif { get; set; } = true;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime? DerniereConnexion { get; set; }

        // ----- Relations -----
        public virtual ICollection<ZoneGeographique> Zones { get; set; } = new List<ZoneGeographique>();
        public virtual ICollection<Appareil> Appareils { get; set; } = new List<Appareil>();
    }
}
