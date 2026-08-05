// ============================================================
// GEO-15 : Tests Unitaires CRUD Véhicule + Apparition Carte
// Story : En tant qu'administrateur, je souhaite ajouter
//         facilement un nouveau véhicule à la flotte
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace GeoTrack.Api.Tests
{
    // ============================================================
    // ENUMS (miroir du service GEO-15)
    // ============================================================
    public enum TypeVehicule { Camion, Voiture, Fourgonnette, Moto, Autobus, Remorque, Autre }
    public enum StatutVehicule { EnAttente, Actif, Inactif, Maintenance, HorsService }
    public enum StatutGPS { NonConfigure, EnAttente, Connecte, Deconnecte }

    // ============================================================
    // MODÈLES SIMPLIFIÉS POUR TESTS
    // ============================================================
    public class Vehicule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Immatriculation { get; set; } = string.Empty;
        public string? Vin { get; set; }
        public string Marque { get; set; } = string.Empty;
        public string Modele { get; set; } = string.Empty;
        public int Annee { get; set; }
        public TypeVehicule Type { get; set; }
        public string? TrackerGpsId { get; set; }
        public string? Conducteur { get; set; }
        public string? Groupe { get; set; }
        public double? SeuilVitesseMax { get; set; }
        public string? ZoneParDefaut { get; set; }
        public string? Notes { get; set; }
        public StatutVehicule Statut { get; set; } = StatutVehicule.EnAttente;
        public StatutGPS StatutGps { get; set; } = StatutGPS.NonConfigure;
        public double? DerniereLatitude { get; set; }
        public double? DerniereLongitude { get; set; }
        public DateTime? DernierePositionDate { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public bool VisibleSurCarte { get; set; } = false;
    }

    public class CreerVehiculeRequest
    {
        public string Immatriculation { get; set; } = string.Empty;
        public string? Vin { get; set; }
        public string Marque { get; set; } = string.Empty;
        public string Modele { get; set; } = string.Empty;
        public int Annee { get; set; }
        public TypeVehicule Type { get; set; }
        public string? TrackerGpsId { get; set; }
        public string? Conducteur { get; set; }
        public string? Groupe { get; set; }
        public double? SeuilVitesseMax { get; set; }
        public string? ZoneParDefaut { get; set; }
        public string? Notes { get; set; }
    }

    public class PositionGpsEvent
    {
        public string TrackerGpsId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Vitesse { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ResultatOperation<T>
    {
        public bool Succes { get; set; }
        public T? Donnees { get; set; }
        public string? Message { get; set; }
        public List<string> Erreurs { get; set; } = new();

        public static ResultatOperation<T> Ok(T donnees, string? message = null) =>
            new() { Succes = true, Donnees = donnees, Message = message };

        public static ResultatOperation<T> Echec(string message, List<string>? erreurs = null) =>
            new() { Succes = false, Message = message, Erreurs = erreurs ?? new() };
    }

    // ============================================================
    // SERVICE SIMPLIFIÉ POUR TESTS
    // ============================================================
    public class VehiculeServiceTest
    {
        private readonly List<Vehicule> _vehicules = new();

        public ResultatOperation<Vehicule> Creer(CreerVehiculeRequest request)
        {
            var erreurs = new List<string>();

            // Validations requises
            if (string.IsNullOrWhiteSpace(request.Immatriculation))
                erreurs.Add("L'immatriculation est requise.");
            if (string.IsNullOrWhiteSpace(request.Marque))
                erreurs.Add("La marque est requise.");
            if (string.IsNullOrWhiteSpace(request.Modele))
                erreurs.Add("Le modèle est requis.");
            if (request.Annee < 1900 || request.Annee > DateTime.UtcNow.Year + 1)
                erreurs.Add($"L'année doit être entre 1900 et {DateTime.UtcNow.Year + 1}.");

            // Unicité immatriculation
            if (_vehicules.Any(v => v.Immatriculation == request.Immatriculation))
                erreurs.Add("Un véhicule avec cette immatriculation existe déjà.");

            // Unicité VIN
            if (!string.IsNullOrWhiteSpace(request.Vin) &&
                _vehicules.Any(v => v.Vin == request.Vin))
                erreurs.Add("Un véhicule avec ce VIN existe déjà.");

            // Unicité tracker GPS
            if (!string.IsNullOrWhiteSpace(request.TrackerGpsId) &&
                _vehicules.Any(v => v.TrackerGpsId == request.TrackerGpsId))
                erreurs.Add("Ce tracker GPS est déjà assigné à un autre véhicule.");

            // Seuil vitesse positif
            if (request.SeuilVitesseMax.HasValue && request.SeuilVitesseMax <= 0)
                erreurs.Add("Le seuil de vitesse doit être positif.");

            if (erreurs.Count > 0)
                return ResultatOperation<Vehicule>.Echec("Validation échouée.", erreurs);

            var vehicule = new Vehicule
            {
                Immatriculation = request.Immatriculation,
                Vin = request.Vin,
                Marque = request.Marque,
                Modele = request.Modele,
                Annee = request.Annee,
                Type = request.Type,
                TrackerGpsId = request.TrackerGpsId,
                Conducteur = request.Conducteur,
                Groupe = request.Groupe,
                SeuilVitesseMax = request.SeuilVitesseMax,
                ZoneParDefaut = request.ZoneParDefaut,
                Notes = request.Notes,
                Statut = string.IsNullOrWhiteSpace(request.TrackerGpsId)
                    ? StatutVehicule.EnAttente
                    : StatutVehicule.EnAttente,
                StatutGps = string.IsNullOrWhiteSpace(request.TrackerGpsId)
                    ? StatutGPS.NonConfigure
                    : StatutGPS.EnAttente
            };

            _vehicules.Add(vehicule);
            return ResultatOperation<Vehicule>.Ok(vehicule, "Véhicule créé avec succès.");
        }

        public ResultatOperation<Vehicule> Supprimer(string id)
        {
            var vehicule = _vehicules.FirstOrDefault(v => v.Id == id);
            if (vehicule == null)
                return ResultatOperation<Vehicule>.Echec("Véhicule non trouvé.");

            if (vehicule.Statut == StatutVehicule.Actif)
                return ResultatOperation<Vehicule>.Echec("Impossible de supprimer un véhicule actif.");

            _vehicules.Remove(vehicule);
            return ResultatOperation<Vehicule>.Ok(vehicule, "Véhicule supprimé.");
        }

        public ResultatOperation<Vehicule> RecevoirPositionGps(PositionGpsEvent evt)
        {
            var vehicule = _vehicules.FirstOrDefault(v => v.TrackerGpsId == evt.TrackerGpsId);
            if (vehicule == null)
                return ResultatOperation<Vehicule>.Echec("Aucun véhicule associé à ce tracker.");

            bool premierePosition = !vehicule.DerniereLatitude.HasValue;

            vehicule.DerniereLatitude = evt.Latitude;
            vehicule.DerniereLongitude = evt.Longitude;
            vehicule.DernierePositionDate = evt.Timestamp;
            vehicule.StatutGps = StatutGPS.Connecte;

            if (premierePosition)
            {
                vehicule.Statut = StatutVehicule.Actif;
                vehicule.VisibleSurCarte = true;
            }

            return ResultatOperation<Vehicule>.Ok(vehicule,
                premierePosition
                    ? "Première position GPS reçue. Véhicule visible sur la carte."
                    : "Position GPS mise à jour.");
        }

        public List<Vehicule> ListerVisiblesSurCarte() =>
            _vehicules.Where(v => v.VisibleSurCarte).ToList();

        public bool VerifierUniciteImmatriculation(string immatriculation) =>
            !_vehicules.Any(v => v.Immatriculation == immatriculation);

        public bool VerifierUniciteTracker(string trackerId) =>
            !_vehicules.Any(v => v.TrackerGpsId == trackerId);
    }

    // ============================================================
    // TESTS UNITAIRES — 24 CAS
    // ============================================================
    public class GEO15_VehiculeCrud_Tests
    {
        private readonly VehiculeServiceTest _service = new();

        private CreerVehiculeRequest RequestValide() => new()
        {
            Immatriculation = "QC-" + Guid.NewGuid().ToString()[..6],
            Vin = "1HGBH41JXMN" + new Random().Next(100000, 999999),
            Marque = "Toyota",
            Modele = "Corolla",
            Annee = 2024,
            Type = TypeVehicule.Voiture,
            TrackerGpsId = "TRK-" + Guid.NewGuid().ToString()[..8],
            Conducteur = "Jean Dupont",
            Groupe = "Division Nord",
            SeuilVitesseMax = 120.0,
            ZoneParDefaut = "zone-gatineau-centre",
            Notes = "Véhicule de test"
        };

        // ========================================
        // CATÉGORIE 1 : Création véhicule (8 tests)
        // ========================================

        [Fact]
        public void Creer_RequestValide_RetourneSucces()
        {
            var request = RequestValide();
            var resultat = _service.Creer(request);

            Assert.True(resultat.Succes);
            Assert.NotNull(resultat.Donnees);
            Assert.Equal(request.Immatriculation, resultat.Donnees!.Immatriculation);
            Assert.Equal(request.Marque, resultat.Donnees.Marque);
        }

        [Fact]
        public void Creer_StatutInitial_EnAttente()
        {
            var resultat = _service.Creer(RequestValide());

            Assert.True(resultat.Succes);
            Assert.Equal(StatutVehicule.EnAttente, resultat.Donnees!.Statut);
            Assert.False(resultat.Donnees.VisibleSurCarte);
        }

        [Fact]
        public void Creer_AvecTracker_StatutGpsEnAttente()
        {
            var request = RequestValide();
            request.TrackerGpsId = "TRK-GPS-001";
            var resultat = _service.Creer(request);

            Assert.True(resultat.Succes);
            Assert.Equal(StatutGPS.EnAttente, resultat.Donnees!.StatutGps);
        }

        [Fact]
        public void Creer_SansTracker_StatutGpsNonConfigure()
        {
            var request = RequestValide();
            request.TrackerGpsId = null;
            var resultat = _service.Creer(request);

            Assert.True(resultat.Succes);
            Assert.Equal(StatutGPS.NonConfigure, resultat.Donnees!.StatutGps);
        }

        [Fact]
        public void Creer_ImmatriculationVide_RetourneErreur()
        {
            var request = RequestValide();
            request.Immatriculation = "";
            var resultat = _service.Creer(request);

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("immatriculation"));
        }

        [Fact]
        public void Creer_MarqueVide_RetourneErreur()
        {
            var request = RequestValide();
            request.Marque = "";
            var resultat = _service.Creer(request);

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("marque"));
        }

        [Fact]
        public void Creer_AnneeInvalide_RetourneErreur()
        {
            var request = RequestValide();
            request.Annee = 1800;
            var resultat = _service.Creer(request);

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("année"));
        }

        [Fact]
        public void Creer_SeuilVitesseNegatif_RetourneErreur()
        {
            var request = RequestValide();
            request.SeuilVitesseMax = -10;
            var resultat = _service.Creer(request);

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("vitesse"));
        }

        // ========================================
        // CATÉGORIE 2 : Unicité (5 tests)
        // ========================================

        [Fact]
        public void Creer_ImmatriculationDupliquee_RetourneErreur()
        {
            var request1 = RequestValide();
            _service.Creer(request1);

            var request2 = RequestValide();
            request2.Immatriculation = request1.Immatriculation;
            var resultat = _service.Creer(request2);

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("immatriculation existe"));
        }

        [Fact]
        public void Creer_VinDuplique_RetourneErreur()
        {
            var request1 = RequestValide();
            _service.Creer(request1);

            var request2 = RequestValide();
            request2.Vin = request1.Vin;
            var resultat = _service.Creer(request2);

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("VIN existe"));
        }

        [Fact]
        public void Creer_TrackerDuplique_RetourneErreur()
        {
            var request1 = RequestValide();
            _service.Creer(request1);

            var request2 = RequestValide();
            request2.TrackerGpsId = request1.TrackerGpsId;
            var resultat = _service.Creer(request2);

            Assert.False(resultat.Succes);
            Assert.Contains(resultat.Erreurs, e => e.Contains("tracker GPS"));
        }

        [Fact]
        public void VerifierUniciteImmatriculation_Disponible_RetourneTrue()
        {
            Assert.True(_service.VerifierUniciteImmatriculation("QC-UNIQUE-001"));
        }

        [Fact]
        public void VerifierUniciteTracker_Disponible_RetourneTrue()
        {
            Assert.True(_service.VerifierUniciteTracker("TRK-UNIQUE-001"));
        }

        // ========================================
        // CATÉGORIE 3 : Position GPS + Apparition carte (6 tests)
        // ========================================

        [Fact]
        public void RecevoirGps_PremierePosition_VehiculeActifEtVisibleCarte()
        {
            var request = RequestValide();
            request.TrackerGpsId = "TRK-CARTE-001";
            _service.Creer(request);

            var evt = new PositionGpsEvent
            {
                TrackerGpsId = "TRK-CARTE-001",
                Latitude = 45.4765,
                Longitude = -75.7013,
                Vitesse = 30.0
            };

            var resultat = _service.RecevoirPositionGps(evt);

            Assert.True(resultat.Succes);
            Assert.Equal(StatutVehicule.Actif, resultat.Donnees!.Statut);
            Assert.True(resultat.Donnees.VisibleSurCarte);
            Assert.Contains("Première position", resultat.Message!);
        }

        [Fact]
        public void RecevoirGps_PremierePosition_CoordonneesStockees()
        {
            var request = RequestValide();
            request.TrackerGpsId = "TRK-COORD-001";
            _service.Creer(request);

            var evt = new PositionGpsEvent
            {
                TrackerGpsId = "TRK-COORD-001",
                Latitude = 45.4765,
                Longitude = -75.7013
            };

            var resultat = _service.RecevoirPositionGps(evt);

            Assert.Equal(45.4765, resultat.Donnees!.DerniereLatitude);
            Assert.Equal(-75.7013, resultat.Donnees.DerniereLongitude);
            Assert.Equal(StatutGPS.Connecte, resultat.Donnees.StatutGps);
        }

        [Fact]
        public void RecevoirGps_DeuxiemePosition_MiseAJourSansChangerStatut()
        {
            var request = RequestValide();
            request.TrackerGpsId = "TRK-MAJ-001";
            _service.Creer(request);

            // Première position
            _service.RecevoirPositionGps(new PositionGpsEvent
            {
                TrackerGpsId = "TRK-MAJ-001",
                Latitude = 45.4765,
                Longitude = -75.7013
            });

            // Deuxième position
            var resultat = _service.RecevoirPositionGps(new PositionGpsEvent
            {
                TrackerGpsId = "TRK-MAJ-001",
                Latitude = 45.4800,
                Longitude = -75.6900
            });

            Assert.True(resultat.Succes);
            Assert.Equal(45.4800, resultat.Donnees!.DerniereLatitude);
            Assert.Contains("mise à jour", resultat.Message!);
        }

        [Fact]
        public void RecevoirGps_TrackerInconnu_RetourneErreur()
        {
            var evt = new PositionGpsEvent
            {
                TrackerGpsId = "TRK-INEXISTANT",
                Latitude = 45.4765,
                Longitude = -75.7013
            };

            var resultat = _service.RecevoirPositionGps(evt);

            Assert.False(resultat.Succes);
            Assert.Contains("Aucun véhicule", resultat.Message!);
        }

        [Fact]
        public void ListerVisiblesSurCarte_ApresPremierePosition_ContientVehicule()
        {
            var request = RequestValide();
            request.TrackerGpsId = "TRK-VISIBLE-001";
            _service.Creer(request);

            _service.RecevoirPositionGps(new PositionGpsEvent
            {
                TrackerGpsId = "TRK-VISIBLE-001",
                Latitude = 45.4765,
                Longitude = -75.7013
            });

            var visibles = _service.ListerVisiblesSurCarte();
            Assert.Single(visibles);
            Assert.Equal("TRK-VISIBLE-001", visibles[0].TrackerGpsId);
        }

        [Fact]
        public void ListerVisiblesSurCarte_SansPosition_ListeVide()
        {
            _service.Creer(RequestValide());
            var visibles = _service.ListerVisiblesSurCarte();
            Assert.Empty(visibles);
        }

        // ========================================
        // CATÉGORIE 4 : Suppression (3 tests)
        // ========================================

        [Fact]
        public void Supprimer_VehiculeEnAttente_RetourneSucces()
        {
            var request = RequestValide();
            var creation = _service.Creer(request);
            var id = creation.Donnees!.Id;

            var resultat = _service.Supprimer(id);

            Assert.True(resultat.Succes);
            Assert.Contains("supprimé", resultat.Message!);
        }

        [Fact]
        public void Supprimer_VehiculeActif_RetourneErreur()
        {
            var request = RequestValide();
            request.TrackerGpsId = "TRK-SUPP-001";
            var creation = _service.Creer(request);

            // Rendre actif via GPS
            _service.RecevoirPositionGps(new PositionGpsEvent
            {
                TrackerGpsId = "TRK-SUPP-001",
                Latitude = 45.4765,
                Longitude = -75.7013
            });

            var resultat = _service.Supprimer(creation.Donnees!.Id);

            Assert.False(resultat.Succes);
            Assert.Contains("actif", resultat.Message!);
        }

        [Fact]
        public void Supprimer_IdInexistant_RetourneErreur()
        {
            var resultat = _service.Supprimer("id-inexistant");

            Assert.False(resultat.Succes);
            Assert.Contains("non trouvé", resultat.Message!);
        }

        // ========================================
        // CATÉGORIE 5 : Intégration GEO-9/GEO-10 (2 tests)
        // ========================================

        [Fact]
        public void Creer_AvecSeuilVitesse_StockeCorrectement()
        {
            var request = RequestValide();
            request.SeuilVitesseMax = 90.0;
            request.ZoneParDefaut = "zone-gatineau-centre";

            var resultat = _service.Creer(request);

            Assert.True(resultat.Succes);
            Assert.Equal(90.0, resultat.Donnees!.SeuilVitesseMax);
            Assert.Equal("zone-gatineau-centre", resultat.Donnees.ZoneParDefaut);
        }

        [Fact]
        public void Creer_TousLesTypes_AccepteToutesValeurs()
        {
            foreach (TypeVehicule type in Enum.GetValues<TypeVehicule>())
            {
                var request = RequestValide();
                request.Type = type;
                var resultat = _service.Creer(request);

                Assert.True(resultat.Succes);
                Assert.Equal(type, resultat.Donnees!.Type);
            }
        }
    }
}
