// =============================================================================
// GEO-12 : Tests Unitaires — Historique Trajets
// Projet : GeoTrack (GEN1423 – Groupe 8)
// Auteur : Sory Fofana
// Date : 2026-08-05
// Framework : xUnit + FluentAssertions
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using GeoTrack.Api.Services;

namespace GeoTrack.Api.Tests
{
    public class HistoriqueTrajetServiceTests
    {
        private readonly HistoriqueTrajetService _service;

        public HistoriqueTrajetServiceTests()
        {
            _service = new HistoriqueTrajetService();
        }

        // =====================================================================
        // CATÉGORIE 1 : LISTE TRAJETS PAGINÉE (7 tests)
        // Critère #1 : L'historique affiche les trajets avec date et durée
        // =====================================================================

        [Fact]
        public void Lister_RetourneTrajetsAvecDateEtDuree()
        {
            // Arrange
            var requete = new RequeteListeTrajets
            {
                VehiculeId = "VEH-001",
                Page = 1,
                ParPage = 10
            };

            // Act
            var resultat = _service.ListerTrajets(requete);

            // Assert — Critère #1 : chaque trajet a date et durée
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.Trajets.Should().NotBeEmpty();
            foreach (var trajet in resultat.Donnees.Trajets)
            {
                trajet.DateDebut.Should().NotBe(default);
                trajet.DureeFormatee.Should().NotBeNullOrEmpty();
                trajet.DureeFormatee.Should().MatchRegex(@"\d+h\s*\d+min");
            }
        }

        [Fact]
        public void Lister_Pagination_RetournePageDemandee()
        {
            // Arrange
            var requete = new RequeteListeTrajets
            {
                VehiculeId = "VEH-001",
                Page = 2,
                ParPage = 5
            };

            // Act
            var resultat = _service.ListerTrajets(requete);

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.PageCourante.Should().Be(2);
            resultat.Donnees.ParPage.Should().Be(5);
            resultat.Donnees.Trajets.Count.Should().BeLessOrEqualTo(5);
        }

        [Fact]
        public void Lister_FiltreParDate_RetourneTrajetsEntreDates()
        {
            // Arrange
            var dateDebut = new DateTime(2026, 7, 1);
            var dateFin = new DateTime(2026, 7, 31);
            var requete = new RequeteListeTrajets
            {
                VehiculeId = "VEH-001",
                DateDebut = dateDebut,
                DateFin = dateFin,
                Page = 1,
                ParPage = 10
            };

            // Act
            var resultat = _service.ListerTrajets(requete);

            // Assert
            resultat.Succes.Should().BeTrue();
            foreach (var trajet in resultat.Donnees.Trajets)
            {
                trajet.DateDebut.Should().BeOnOrAfter(dateDebut);
                trajet.DateDebut.Should().BeOnOrBefore(dateFin);
            }
        }

        [Fact]
        public void Lister_FiltreParVehicule_RetourneUniquementCeVehicule()
        {
            // Arrange
            var requete = new RequeteListeTrajets
            {
                VehiculeId = "VEH-002",
                Page = 1,
                ParPage = 10
            };

            // Act
            var resultat = _service.ListerTrajets(requete);

            // Assert
            resultat.Succes.Should().BeTrue();
            foreach (var trajet in resultat.Donnees.Trajets)
            {
                trajet.VehiculeId.Should().Be("VEH-002");
            }
        }

        [Fact]
        public void Lister_TriParDateDescendant_OrdreCorrect()
        {
            // Arrange
            var requete = new RequeteListeTrajets
            {
                VehiculeId = "VEH-001",
                Tri = "date_desc",
                Page = 1,
                ParPage = 10
            };

            // Act
            var resultat = _service.ListerTrajets(requete);

            // Assert
            resultat.Succes.Should().BeTrue();
            var dates = resultat.Donnees.Trajets.Select(t => t.DateDebut).ToList();
            dates.Should().BeInDescendingOrder();
        }

        [Fact]
        public void Lister_TriParDistanceAscendant_OrdreCorrect()
        {
            // Arrange
            var requete = new RequeteListeTrajets
            {
                VehiculeId = "VEH-001",
                Tri = "distance_asc",
                Page = 1,
                ParPage = 10
            };

            // Act
            var resultat = _service.ListerTrajets(requete);

            // Assert
            resultat.Succes.Should().BeTrue();
            var distances = resultat.Donnees.Trajets.Select(t => t.DistanceKm).ToList();
            distances.Should().BeInAscendingOrder();
        }

        [Fact]
        public void Lister_VehiculeInexistant_RetourneListeVide()
        {
            // Arrange
            var requete = new RequeteListeTrajets
            {
                VehiculeId = "VEH-INEXISTANT",
                Page = 1,
                ParPage = 10
            };

            // Act
            var resultat = _service.ListerTrajets(requete);

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.Trajets.Should().BeEmpty();
            resultat.Donnees.TotalTrajets.Should().Be(0);
        }

        // =====================================================================
        // CATÉGORIE 2 : VISUALISATION CARTE (6 tests)
        // Critère #2 : Le trajet peut être visualisé sur une carte
        // =====================================================================

        [Fact]
        public void ObtenirCarte_RetournePointsGpsEtGradientVitesse()
        {
            // Arrange
            var trajetId = "TRJ-001";

            // Act
            var resultat = _service.ObtenirCarteTrajets(trajetId);

            // Assert — Critère #2 : points GPS + gradient vitesse
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.PointsGps.Should().NotBeEmpty();
            foreach (var point in resultat.Donnees.PointsGps)
            {
                point.Latitude.Should().BeInRange(-90, 90);
                point.Longitude.Should().BeInRange(-180, 180);
                point.Vitesse.Should().BeGreaterOrEqualTo(0);
                point.CouleurGradient.Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public void ObtenirCarte_ContientMarqueursDebutFin()
        {
            // Arrange
            var trajetId = "TRJ-001";

            // Act
            var resultat = _service.ObtenirCarteTrajets(trajetId);

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.MarqueurDepart.Should().NotBeNull();
            resultat.Donnees.MarqueurArrivee.Should().NotBeNull();
            resultat.Donnees.MarqueurDepart.Label.Should().Be("A");
            resultat.Donnees.MarqueurArrivee.Label.Should().Be("B");
        }

        [Fact]
        public void ObtenirCarte_ContientBoundingBox()
        {
            // Arrange
            var trajetId = "TRJ-001";

            // Act
            var resultat = _service.ObtenirCarteTrajets(trajetId);

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.BoundingBox.Should().NotBeNull();
            resultat.Donnees.BoundingBox.NordEst.Latitude.Should()
                .BeGreaterThan(resultat.Donnees.BoundingBox.SudOuest.Latitude);
            resultat.Donnees.BoundingBox.NordEst.Longitude.Should()
                .BeGreaterThan(resultat.Donnees.BoundingBox.SudOuest.Longitude);
        }

        [Fact]
        public void ObtenirCarte_GradientVitesse_CouleursCorrectes()
        {
            // Arrange
            var trajetId = "TRJ-001";

            // Act
            var resultat = _service.ObtenirCarteTrajets(trajetId);

            // Assert — Couleurs selon seuils
            var couleursValides = new[] { "#2ecc71", "#f1c40f", "#e67e22", "#e74c3c" };
            foreach (var point in resultat.Donnees.PointsGps)
            {
                couleursValides.Should().Contain(point.CouleurGradient);
            }
        }

        [Fact]
        public void ObtenirCarte_ContientArrets()
        {
            // Arrange
            var trajetId = "TRJ-001";

            // Act
            var resultat = _service.ObtenirCarteTrajets(trajetId);

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.Arrets.Should().NotBeNull();
            foreach (var arret in resultat.Donnees.Arrets)
            {
                arret.Latitude.Should().BeInRange(-90, 90);
                arret.Longitude.Should().BeInRange(-180, 180);
                arret.DureeMinutes.Should().BeGreaterThan(0);
                arret.Type.Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public void ObtenirCarte_TrajetInexistant_RetourneEchec()
        {
            // Arrange
            var trajetId = "TRJ-INEXISTANT";

            // Act
            var resultat = _service.ObtenirCarteTrajets(trajetId);

            // Assert
            resultat.Succes.Should().BeFalse();
            resultat.Message.Should().Contain("introuvable");
        }

        // =====================================================================
        // CATÉGORIE 3 : STATISTIQUES (4 tests)
        // =====================================================================

        [Fact]
        public void ObtenirStats_RetourneStatistiquesCompletes()
        {
            // Arrange
            var vehiculeId = "VEH-001";
            var dateDebut = new DateTime(2026, 7, 1);
            var dateFin = new DateTime(2026, 7, 31);

            // Act
            var resultat = _service.ObtenirStatistiques(vehiculeId, dateDebut, dateFin);

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.DistanceTotaleKm.Should().BeGreaterThan(0);
            resultat.Donnees.DureeTotaleMinutes.Should().BeGreaterThan(0);
            resultat.Donnees.VitesseMoyenneKmh.Should().BeGreaterThan(0);
            resultat.Donnees.NombreTrajets.Should().BeGreaterThan(0);
        }

        [Fact]
        public void ObtenirStats_VitesseMax_SuperieureAMoyenne()
        {
            // Arrange
            var vehiculeId = "VEH-001";
            var dateDebut = new DateTime(2026, 7, 1);
            var dateFin = new DateTime(2026, 7, 31);

            // Act
            var resultat = _service.ObtenirStatistiques(vehiculeId, dateDebut, dateFin);

            // Assert
            resultat.Donnees.VitesseMaxKmh.Should()
                .BeGreaterOrEqualTo(resultat.Donnees.VitesseMoyenneKmh);
        }

        [Fact]
        public void ObtenirStats_NombreArrets_Positif()
        {
            // Arrange
            var vehiculeId = "VEH-001";
            var dateDebut = new DateTime(2026, 7, 1);
            var dateFin = new DateTime(2026, 7, 31);

            // Act
            var resultat = _service.ObtenirStatistiques(vehiculeId, dateDebut, dateFin);

            // Assert
            resultat.Donnees.NombreArrets.Should().BeGreaterOrEqualTo(0);
            resultat.Donnees.TempsArretMinutes.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void ObtenirStats_PeriodeSansTrajets_RetourneZeros()
        {
            // Arrange
            var vehiculeId = "VEH-001";
            var dateDebut = new DateTime(2020, 1, 1);
            var dateFin = new DateTime(2020, 1, 31);

            // Act
            var resultat = _service.ObtenirStatistiques(vehiculeId, dateDebut, dateFin);

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.DistanceTotaleKm.Should().Be(0);
            resultat.Donnees.NombreTrajets.Should().Be(0);
        }

        // =====================================================================
        // CATÉGORIE 4 : EXPORT CSV (3 tests)
        // =====================================================================

        [Fact]
        public void ExporterCsv_RetourneContenuValide()
        {
            // Arrange
            var vehiculeId = "VEH-001";
            var dateDebut = new DateTime(2026, 7, 1);
            var dateFin = new DateTime(2026, 7, 31);

            // Act
            var resultat = _service.ExporterCsv(vehiculeId, dateDebut, dateFin);

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.Contenu.Should().NotBeNullOrEmpty();
            resultat.Donnees.NomFichier.Should().EndWith(".csv");
            resultat.Donnees.ContentType.Should().Be("text/csv");
        }

        [Fact]
        public void ExporterCsv_ContientEntetes()
        {
            // Arrange
            var vehiculeId = "VEH-001";
            var dateDebut = new DateTime(2026, 7, 1);
            var dateFin = new DateTime(2026, 7, 31);

            // Act
            var resultat = _service.ExporterCsv(vehiculeId, dateDebut, dateFin);

            // Assert
            var lignes = resultat.Donnees.Contenu.Split('\n');
            var entete = lignes[0];
            entete.Should().Contain("DateDebut");
            entete.Should().Contain("DateFin");
            entete.Should().Contain("DistanceKm");
            entete.Should().Contain("DureeMinutes");
            entete.Should().Contain("VitesseMoyenne");
        }

        [Fact]
        public void ExporterCsv_EncodageUtf8_CaracteresSpeciaux()
        {
            // Arrange
            var vehiculeId = "VEH-001";
            var dateDebut = new DateTime(2026, 7, 1);
            var dateFin = new DateTime(2026, 7, 31);

            // Act
            var resultat = _service.ExporterCsv(vehiculeId, dateDebut, dateFin);

            // Assert
            resultat.Donnees.Encodage.Should().Be("UTF-8");
            // Vérifier BOM UTF-8 si présent
            resultat.Donnees.Contenu.Should().NotContain("???");
        }

        // =====================================================================
        // CATÉGORIE 5 : DÉTECTION AUTO TRAJETS (4 tests)
        // =====================================================================

        [Fact]
        public void DetecterTrajet_DebutMouvement_CreerNouveauTrajet()
        {
            // Arrange
            var positionGps = new PointGpsEvent
            {
                VehiculeId = "VEH-001",
                Latitude = 45.4765,
                Longitude = -75.7013,
                Vitesse = 15.0, // > seuil 5 km/h
                Timestamp = DateTime.UtcNow
            };

            // Act
            var resultat = _service.TraiterPositionGps(positionGps);

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.NouveauTrajetCree.Should().BeTrue();
            resultat.Donnees.StatutTrajet.Should().Be(StatutTrajet.EnCours);
        }

        [Fact]
        public void DetecterTrajet_ArretProlonge_TerminerTrajet()
        {
            // Arrange — Simuler un arrêt de 5+ minutes
            var positionsArret = new List<PointGpsEvent>();
            var baseTime = DateTime.UtcNow.AddMinutes(-6);
            for (int i = 0; i < 6; i++)
            {
                positionsArret.Add(new PointGpsEvent
                {
                    VehiculeId = "VEH-001",
                    Latitude = 45.4765,
                    Longitude = -75.7013,
                    Vitesse = 0.0,
                    Timestamp = baseTime.AddMinutes(i)
                });
            }

            // Act
            ResultatOperation<DetectionTrajetDto> resultat = null;
            foreach (var pos in positionsArret)
            {
                resultat = _service.TraiterPositionGps(pos);
            }

            // Assert
            resultat.Succes.Should().BeTrue();
            resultat.Donnees.TrajetTermine.Should().BeTrue();
            resultat.Donnees.StatutTrajet.Should().Be(StatutTrajet.Termine);
        }

        [Fact]
        public void DetecterTrajet_DureeMinimale_IgnorerTrajetTropCourt()
        {
            // Arrange — Trajet de moins de 2 minutes
            var debut = new PointGpsEvent
            {
                VehiculeId = "VEH-001",
                Latitude = 45.4765,
                Longitude = -75.7013,
                Vitesse = 20.0,
                Timestamp = DateTime.UtcNow.AddMinutes(-1)
            };
            var fin = new PointGpsEvent
            {
                VehiculeId = "VEH-001",
                Latitude = 45.4766,
                Longitude = -75.7014,
                Vitesse = 0.0,
                Timestamp = DateTime.UtcNow
            };

            // Act
            _service.TraiterPositionGps(debut);
            var resultat = _service.TraiterPositionGps(fin);

            // Assert
            resultat.Donnees.TrajetIgnore.Should().BeTrue();
            resultat.Donnees.Raison.Should().Contain("durée minimale");
        }

        [Fact]
        public void DetecterTrajet_SeuilVitesse_5Kmh()
        {
            // Arrange — Vitesse sous le seuil (3 km/h = pas un mouvement)
            var position = new PointGpsEvent
            {
                VehiculeId = "VEH-001",
                Latitude = 45.4765,
                Longitude = -75.7013,
                Vitesse = 3.0, // < seuil 5 km/h
                Timestamp = DateTime.UtcNow
            };

            // Act
            var resultat = _service.TraiterPositionGps(position);

            // Assert
            resultat.Donnees.NouveauTrajetCree.Should().BeFalse();
            resultat.Donnees.ConsidereArret.Should().BeTrue();
        }

        // =====================================================================
        // CATÉGORIE 6 : DOUGLAS-PEUCKER (2 tests)
        // =====================================================================

        [Fact]
        public void DouglasPeucker_PlusDe10KPoints_Simplifie()
        {
            // Arrange — Générer 15 000 points GPS
            var points = new List<PointGps>();
            for (int i = 0; i < 15000; i++)
            {
                points.Add(new PointGps
                {
                    Latitude = 45.4765 + (i * 0.0001),
                    Longitude = -75.7013 + (i * 0.0001),
                    Vitesse = 50.0,
                    Timestamp = DateTime.UtcNow.AddSeconds(i * 5)
                });
            }

            // Act
            var resultat = _service.SimplifierTrace(points, tolerance: 0.0001);

            // Assert
            resultat.Count.Should().BeLessThan(15000);
            resultat.Count.Should().BeGreaterThan(0);
            // Premier et dernier points toujours conservés
            resultat.First().Latitude.Should().Be(points.First().Latitude);
            resultat.Last().Latitude.Should().Be(points.Last().Latitude);
        }

        [Fact]
        public void DouglasPeucker_MoinsDe10KPoints_PasDeSimplification()
        {
            // Arrange — 500 points (sous le seuil)
            var points = new List<PointGps>();
            for (int i = 0; i < 500; i++)
            {
                points.Add(new PointGps
                {
                    Latitude = 45.4765 + (i * 0.001),
                    Longitude = -75.7013 + (i * 0.001),
                    Vitesse = 40.0,
                    Timestamp = DateTime.UtcNow.AddSeconds(i * 5)
                });
            }

            // Act
            var resultat = _service.SimplifierTrace(points, tolerance: 0.0001);

            // Assert — Pas de simplification nécessaire
            resultat.Count.Should().Be(500);
        }
    }
}
