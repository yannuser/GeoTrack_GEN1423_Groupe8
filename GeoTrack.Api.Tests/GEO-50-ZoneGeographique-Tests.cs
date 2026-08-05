using System;
using Xunit;
using System.Collections.Generic;

// =============================================================================
// GEO-50 : Tests unitaires - ZoneGeographique et entités associées
// Projet  : GeoTrack - GEN1423 Groupe 8
// Auteur  : Sory Fofana
// Ticket  : GEO-50 (Story GEO-9)
// Branche : feature/geo-9-zone-geographique
// =============================================================================

namespace GeoTrack.Api.Tests
{
    // =========================================================================
    // MODÈLES (copies locales pour les tests - à remplacer par les vrais namespaces)
    // =========================================================================

    public enum TypeZone { Inclusion, Exclusion }
    public enum FormeGeometrique { Cercle, Polygone, Rectangle }
    public enum TypeEvenement { Entree, Sortie, DepassementVitesse }
    public enum NiveauSeverite { Info, Avertissement, Critique }
    public enum TypeAppareil { Smartphone, GPS, Tablette }

    public class ZoneGeographique
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TypeZone TypeZone { get; set; }
        public FormeGeometrique FormeGeometrique { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Rayon { get; set; }
        public string? CoordonneesJson { get; set; }
        public bool EstActive { get; set; } = true;
        public bool EstSupprime { get; set; } = false;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime? DateModification { get; set; }
        public int CreePar { get; set; }

        public bool EstValide()
        {
            if (string.IsNullOrWhiteSpace(Nom)) return false;
            if (FormeGeometrique == FormeGeometrique.Cercle)
            {
                if (Latitude == null || Longitude == null || Rayon == null) return false;
                if (Latitude < -90 || Latitude > 90) return false;
                if (Longitude < -180 || Longitude > 180) return false;
                if (Rayon <= 0) return false;
            }
            return true;
        }
    }

    public class RegleAlerte
    {
        public int Id { get; set; }
        public int ZoneGeographiqueId { get; set; }
        public TypeEvenement TypeEvenement { get; set; }
        public double? SeuilVitesse { get; set; }
        public NiveauSeverite Severite { get; set; }
        public int DelaiAntiSpamMinutes { get; set; } = 5;
        public bool EstActive { get; set; } = true;
        public bool EstSupprime { get; set; } = false;

        public bool SeuilVitesseValide()
        {
            if (TypeEvenement == TypeEvenement.DepassementVitesse)
                return SeuilVitesse.HasValue && SeuilVitesse > 0 && SeuilVitesse <= 300;
            return true;
        }
    }

    public class HistoriqueEvenement
    {
        public int Id { get; set; }
        public int ZoneGeographiqueId { get; set; }
        public int AppareilId { get; set; }
        public TypeEvenement TypeEvenement { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Vitesse { get; set; }
        public bool AlerteEnvoyee { get; set; } = false;
        public DateTime DateEvenement { get; set; } = DateTime.UtcNow;

        public bool PositionValide()
        {
            return Latitude >= -90 && Latitude <= 90
                && Longitude >= -180 && Longitude <= 180;
        }
    }

    public class Appareil
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string IdentifiantUnique { get; set; } = string.Empty;
        public TypeAppareil TypeAppareil { get; set; }
        public double? DerniereLatitude { get; set; }
        public double? DerniereLongitude { get; set; }
        public DateTime? DerniereConnexion { get; set; }
        public bool EstActif { get; set; } = true;
        public bool EstSupprime { get; set; } = false;

        public bool IdentifiantValide()
        {
            return !string.IsNullOrWhiteSpace(IdentifiantUnique)
                && IdentifiantUnique.Length >= 8;
        }
    }

    // =========================================================================
    // TESTS - ZoneGeographique
    // =========================================================================

    public class ZoneGeographiqueTests
    {
        [Fact]
        public void CreerZone_CercleValide_DoitEtreValide()
        {
            // Arrange
            var zone = new ZoneGeographique
            {
                Nom = "Campus UQO",
                TypeZone = TypeZone.Inclusion,
                FormeGeometrique = FormeGeometrique.Cercle,
                Latitude = 45.4215,
                Longitude = -75.6972,
                Rayon = 500,
                CreePar = 1
            };

            // Act
            var resultat = zone.EstValide();

            // Assert
            Assert.True(resultat, "une zone cercle avec coordonnées valides doit être valide");
        }

        [Fact]
        public void CreerZone_SansNom_DoitEtreInvalide()
        {
            // Arrange
            var zone = new ZoneGeographique
            {
                Nom = "",
                TypeZone = TypeZone.Inclusion,
                FormeGeometrique = FormeGeometrique.Cercle,
                Latitude = 45.4215,
                Longitude = -75.6972,
                Rayon = 500
            };

            // Act
            var resultat = zone.EstValide();

            // Assert
            Assert.False(resultat, "une zone sans nom doit être invalide");
        }

        [Fact]
        public void CreerZone_LatitudeHorsLimites_DoitEtreInvalide()
        {
            // Arrange
            var zone = new ZoneGeographique
            {
                Nom = "Zone Test",
                FormeGeometrique = FormeGeometrique.Cercle,
                Latitude = 95.0, // invalide > 90
                Longitude = -75.6972,
                Rayon = 100
            };

            // Act
            var resultat = zone.EstValide();

            // Assert
            Assert.False(resultat, "latitude > 90 doit être invalide");
        }

        [Fact]
        public void CreerZone_RayonNegatif_DoitEtreInvalide()
        {
            // Arrange
            var zone = new ZoneGeographique
            {
                Nom = "Zone Test",
                FormeGeometrique = FormeGeometrique.Cercle,
                Latitude = 45.4215,
                Longitude = -75.6972,
                Rayon = -50 // invalide
            };

            // Act
            var resultat = zone.EstValide();

            // Assert
            Assert.False(resultat, "un rayon négatif doit être invalide");
        }

        [Fact]
        public void CreerZone_TypeExclusion_DoitConserverLeType()
        {
            // Arrange
            var zone = new ZoneGeographique
            {
                Nom = "Zone Interdite",
                TypeZone = TypeZone.Exclusion,
                FormeGeometrique = FormeGeometrique.Cercle,
                Latitude = 45.4215,
                Longitude = -75.6972,
                Rayon = 200
            };

            // Assert
            Assert.Equal(TypeZone.Exclusion, zone.TypeZone);
            Assert.True(zone.EstActive, "une zone est active par défaut");
            Assert.False(zone.EstSupprime, "une zone n'est pas supprimée par défaut");
        }

        [Fact]
        public void SupprimerZone_SoftDelete_DoitMarquerCommeSupprime()
        {
            // Arrange
            var zone = new ZoneGeographique { Nom = "Zone à supprimer" };

            // Act
            zone.EstSupprime = true;
            zone.DateModification = DateTime.UtcNow;

            // Assert
            Assert.True(zone.EstSupprime);
            Assert.NotNull(zone.DateModification);
        }
    }

    // =========================================================================
    // TESTS - RegleAlerte
    // =========================================================================

    public class RegleAlerteTests
    {
        [Fact]
        public void RegleAlerte_SeuilVitesseValide_DoitEtreAccepte()
        {
            // Arrange
            var regle = new RegleAlerte
            {
                ZoneGeographiqueId = 1,
                TypeEvenement = TypeEvenement.DepassementVitesse,
                SeuilVitesse = 50.0,
                Severite = NiveauSeverite.Avertissement
            };

            // Act & Assert
            Assert.True(regle.SeuilVitesseValide(), "50 km/h est un seuil valide");
        }

        [Fact]
        public void RegleAlerte_SeuilVitesseNegatif_DoitEtreInvalide()
        {
            // Arrange
            var regle = new RegleAlerte
            {
                TypeEvenement = TypeEvenement.DepassementVitesse,
                SeuilVitesse = -10.0
            };

            // Act & Assert
            Assert.False(regle.SeuilVitesseValide(), "vitesse négative invalide");
        }

        [Fact]
        public void RegleAlerte_SansSeuilPourEntree_DoitEtreValide()
        {
            // Arrange
            var regle = new RegleAlerte
            {
                TypeEvenement = TypeEvenement.Entree,
                SeuilVitesse = null,
                Severite = NiveauSeverite.Info
            };

            // Act & Assert
            Assert.True(regle.SeuilVitesseValide(), "pas de seuil requis pour Entree");
        }

        [Fact]
        public void RegleAlerte_AntiSpamDefaut_DoitEtre5Minutes()
        {
            var regle = new RegleAlerte();
            Assert.Equal(5, regle.DelaiAntiSpamMinutes);
        }

        [Fact]
        public void RegleAlerte_Severite_DoitEtreConfigurable()
        {
            var regle = new RegleAlerte { Severite = NiveauSeverite.Critique };
            Assert.Equal(NiveauSeverite.Critique, regle.Severite);
        }
    }

    // =========================================================================
    // TESTS - HistoriqueEvenement
    // =========================================================================

    public class HistoriqueEvenementTests
    {
        [Fact]
        public void HistoriqueEvenement_PositionValide_CampusUQO()
        {
            // Arrange - Campus UQO Alexandre-Taché, Gatineau
            var evenement = new HistoriqueEvenement
            {
                Latitude = 45.4215,
                Longitude = -75.6972,
                TypeEvenement = TypeEvenement.Entree,
                AppareilId = 1,
                ZoneGeographiqueId = 1
            };

            // Act & Assert
            Assert.True(evenement.PositionValide());
        }

        [Fact]
        public void HistoriqueEvenement_PositionInvalide_DoitEchouer()
        {
            var evenement = new HistoriqueEvenement
            {
                Latitude = 200.0, // invalide
                Longitude = -75.6972
            };

            Assert.False(evenement.PositionValide());
        }

        [Fact]
        public void HistoriqueEvenement_AlerteEnvoyee_DefautFalse()
        {
            var evenement = new HistoriqueEvenement();
            Assert.False(evenement.AlerteEnvoyee);
        }

        [Fact]
        public void HistoriqueEvenement_DateEvenement_DoitEtreDefinie()
        {
            var evenement = new HistoriqueEvenement();
            Assert.NotEqual(default(DateTime), evenement.DateEvenement);
        }
    }

    // =========================================================================
    // TESTS - Appareil
    // =========================================================================

    public class AppareilTests
    {
        [Fact]
        public void Appareil_IdentifiantValide_DoitEtreAccepte()
        {
            var appareil = new Appareil
            {
                Nom = "GPS Sory",
                IdentifiantUnique = "IMEI-123456789",
                TypeAppareil = TypeAppareil.Smartphone
            };

            Assert.True(appareil.IdentifiantValide());
        }

        [Fact]
        public void Appareil_IdentifiantTropCourt_DoitEtreInvalide()
        {
            var appareil = new Appareil
            {
                IdentifiantUnique = "ABC" // moins de 8 caractères
            };

            Assert.False(appareil.IdentifiantValide());
        }

        [Fact]
        public void Appareil_EstActifParDefaut()
        {
            var appareil = new Appareil();
            Assert.True(appareil.EstActif);
            Assert.False(appareil.EstSupprime);
        }
    }
}
