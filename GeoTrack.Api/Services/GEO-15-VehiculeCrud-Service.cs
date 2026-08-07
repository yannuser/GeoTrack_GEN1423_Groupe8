// ============================================================
// GEO-15 : Service CRUD Véhicule + Apparition Carte GPS
// Story : En tant qu'administrateur, je souhaite ajouter
//         facilement un nouveau véhicule à la flotte afin
//         de suivre son évolution.
// Auteur : Sory Fofana
// Branche : feature/geo-15-ajout-vehicule
// ============================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace GeoTrack.Api.Services
{
    // ============================================================
    // ENUMS
    // ============================================================

    public enum TypeVehicule
    {
        Voiture,
        Camion,
        Fourgonnette,
        Autobus,
        Moto,
        Utilitaire,
        Remorque
    }

    public enum StatutVehicule
    {
        EnAttente,      // Ajouté mais aucune position GPS reçue
        Actif,          // Position GPS reçue, visible sur la carte
        EnMaintenance,
        HorsService,
        Inactif
    }

    public enum StatutGPS
    {
        NonConnecte,
        Connecte,
        PremierePositionRecue,
        SignalPerdu
    }

    // ============================================================
    // MODÈLES
    // ============================================================

    public class Vehicule
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "L'immatriculation est obligatoire")]
        [StringLength(10, MinimumLength = 2)]
        public string Immatriculation { get; set; }

        [StringLength(17, MinimumLength = 17, ErrorMessage = "Le VIN doit contenir exactement 17 caractères")]
        public string VIN { get; set; }

        [Required(ErrorMessage = "La marque est obligatoire")]
        [StringLength(50)]
        public string Marque { get; set; }

        [Required(ErrorMessage = "Le modèle est obligatoire")]
        [StringLength(50)]
        public string Modele { get; set; }

        [Range(1990, 2030)]
        public int Annee { get; set; }

        [Required]
        public TypeVehicule Type { get; set; }

        [Required(ErrorMessage = "L'ID du tracker GPS est obligatoire")]
        public string TrackerGpsId { get; set; }

        public string FournisseurGps { get; set; }

        public StatutVehicule Statut { get; set; } = StatutVehicule.EnAttente;

        public StatutGPS StatutGps { get; set; } = StatutGPS.NonConnecte;

        public int? ConducteurId { get; set; }
        public string ConducteurNom { get; set; }

        public string GroupeDivision { get; set; }

        // Seuils alerte (intégration GEO-9 / GEO-10)
        public double? VitesseMaxKmh { get; set; }
        public int? ZoneParDefautId { get; set; }

        public string Notes { get; set; }

        // Première position GPS
        public double? PremiereLat { get; set; }
        public double? PremiereLng { get; set; }
        public DateTime? PremierePositionDate { get; set; }

        // Métadonnées
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime? DateModification { get; set; }
        public string CreePar { get; set; }
    }

    public class CreerVehiculeRequest
    {
        [Required(ErrorMessage = "L'immatriculation est obligatoire")]
        [StringLength(10, MinimumLength = 2)]
        public string Immatriculation { get; set; }

        [StringLength(17, MinimumLength = 17)]
        public string VIN { get; set; }

        [Required(ErrorMessage = "La marque est obligatoire")]
        public string Marque { get; set; }

        [Required(ErrorMessage = "Le modèle est obligatoire")]
        public string Modele { get; set; }

        [Range(1990, 2030)]
        public int Annee { get; set; }

        [Required]
        public TypeVehicule Type { get; set; }

        [Required(ErrorMessage = "L'ID du tracker GPS est obligatoire")]
        public string TrackerGpsId { get; set; }

        public string FournisseurGps { get; set; }

        public int? ConducteurId { get; set; }

        public string GroupeDivision { get; set; }

        public double? VitesseMaxKmh { get; set; }

        public int? ZoneParDefautId { get; set; }

        public string Notes { get; set; }
    }

    public class ModifierVehiculeRequest
    {
        public string Marque { get; set; }
        public string Modele { get; set; }
        public int? Annee { get; set; }
        public TypeVehicule? Type { get; set; }
        public string TrackerGpsId { get; set; }
        public string FournisseurGps { get; set; }
        public int? ConducteurId { get; set; }
        public string GroupeDivision { get; set; }
        public double? VitesseMaxKmh { get; set; }
        public int? ZoneParDefautId { get; set; }
        public string Notes { get; set; }
        public StatutVehicule? Statut { get; set; }
    }

    public class PositionGpsEvent
    {
        [Required]
        public string TrackerGpsId { get; set; }

        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }

        public double? VitesseKmh { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class VehiculeCarteDto
    {
        public int Id { get; set; }
        public string Immatriculation { get; set; }
        public string Marque { get; set; }
        public string Modele { get; set; }
        public TypeVehicule Type { get; set; }
        public StatutVehicule Statut { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? VitesseKmh { get; set; }
        public DateTime DernierePosition { get; set; }
        public string ConducteurNom { get; set; }
    }

    public class ResultatOperation<T>
    {
        public bool Succes { get; set; }
        public T Donnees { get; set; }
        public string Message { get; set; }
        public List<string> Erreurs { get; set; } = new List<string>();

        public static ResultatOperation<T> Ok(T donnees, string message = null)
            => new ResultatOperation<T> { Succes = true, Donnees = donnees, Message = message };

        public static ResultatOperation<T> Echec(string message, List<string> erreurs = null)
            => new ResultatOperation<T> { Succes = false, Message = message, Erreurs = erreurs ?? new List<string>() };
    }

    // ============================================================
    // INTERFACE REPOSITORY
    // ============================================================

    public interface IVehiculeRepository
    {
        Task<Vehicule> GetByIdAsync(int id);
        Task<Vehicule> GetByImmatriculationAsync(string immatriculation);
        Task<Vehicule> GetByTrackerGpsIdAsync(string trackerGpsId);
        Task<Vehicule> GetByVINAsync(string vin);
        Task<List<Vehicule>> GetAllAsync();
        Task<List<Vehicule>> GetByStatutAsync(StatutVehicule statut);
        Task<Vehicule> CreateAsync(Vehicule vehicule);
        Task<Vehicule> UpdateAsync(Vehicule vehicule);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsImmatriculationAsync(string immatriculation, int? excludeId = null);
        Task<bool> ExistsTrackerGpsIdAsync(string trackerGpsId, int? excludeId = null);
        Task<bool> ExistsVINAsync(string vin, int? excludeId = null);
    }

    public interface IConducteurRepository
    {
        Task<List<(int Id, string Nom)>> GetDisponiblesAsync();
    }

    public interface IGroupeRepository
    {
        Task<List<string>> GetAllAsync();
    }

    // ============================================================
    // SERVICE CRUD VÉHICULE
    // ============================================================

    public class VehiculeService
    {
        private readonly IVehiculeRepository _vehiculeRepo;
        private readonly IConducteurRepository _conducteurRepo;
        private readonly IGroupeRepository _groupeRepo;

        public VehiculeService(
            IVehiculeRepository vehiculeRepo,
            IConducteurRepository conducteurRepo,
            IGroupeRepository groupeRepo)
        {
            _vehiculeRepo = vehiculeRepo;
            _conducteurRepo = conducteurRepo;
            _groupeRepo = groupeRepo;
        }

        // --------------------------------------------------------
        // CRÉER UN VÉHICULE
        // --------------------------------------------------------
        public async Task<ResultatOperation<Vehicule>> CreerVehiculeAsync(
            CreerVehiculeRequest request, string creePar)
        {
            // Validation unicité
            var erreurs = new List<string>();

            if (await _vehiculeRepo.ExistsImmatriculationAsync(request.Immatriculation))
                erreurs.Add($"L'immatriculation '{request.Immatriculation}' existe déjà.");

            if (await _vehiculeRepo.ExistsTrackerGpsIdAsync(request.TrackerGpsId))
                erreurs.Add($"Le tracker GPS '{request.TrackerGpsId}' est déjà assigné à un autre véhicule.");

            if (!string.IsNullOrEmpty(request.VIN) && await _vehiculeRepo.ExistsVINAsync(request.VIN))
                erreurs.Add($"Le VIN '{request.VIN}' existe déjà.");

            if (erreurs.Any())
                return ResultatOperation<Vehicule>.Echec("Erreurs de validation", erreurs);

            // Validation vitesse max
            if (request.VitesseMaxKmh.HasValue && (request.VitesseMaxKmh < 10 || request.VitesseMaxKmh > 200))
                return ResultatOperation<Vehicule>.Echec("La vitesse max doit être entre 10 et 200 km/h.");

            // Créer le véhicule
            var vehicule = new Vehicule
            {
                Immatriculation = request.Immatriculation.ToUpperInvariant().Trim(),
                VIN = request.VIN?.ToUpperInvariant().Trim(),
                Marque = request.Marque.Trim(),
                Modele = request.Modele.Trim(),
                Annee = request.Annee,
                Type = request.Type,
                TrackerGpsId = request.TrackerGpsId.Trim(),
                FournisseurGps = request.FournisseurGps?.Trim(),
                ConducteurId = request.ConducteurId,
                GroupeDivision = request.GroupeDivision?.Trim(),
                VitesseMaxKmh = request.VitesseMaxKmh,
                ZoneParDefautId = request.ZoneParDefautId,
                Notes = request.Notes?.Trim(),
                Statut = StatutVehicule.EnAttente,
                StatutGps = StatutGPS.NonConnecte,
                DateCreation = DateTime.UtcNow,
                CreePar = creePar
            };

            var resultat = await _vehiculeRepo.CreateAsync(vehicule);
            return ResultatOperation<Vehicule>.Ok(resultat,
                $"Véhicule '{resultat.Immatriculation}' ajouté avec succès. En attente de la première position GPS.");
        }

        // --------------------------------------------------------
        // RÉCEPTION PREMIÈRE POSITION GPS → APPARITION CARTE
        // Critère d'acceptation #2 : le véhicule apparaît sur
        // la carte dès la réception de sa première position GPS
        // --------------------------------------------------------
        public async Task<ResultatOperation<VehiculeCarteDto>> TraiterPositionGpsAsync(
            PositionGpsEvent positionEvent)
        {
            var vehicule = await _vehiculeRepo.GetByTrackerGpsIdAsync(positionEvent.TrackerGpsId);

            if (vehicule == null)
                return ResultatOperation<VehiculeCarteDto>.Echec(
                    $"Aucun véhicule associé au tracker '{positionEvent.TrackerGpsId}'.");

            bool estPremierePosition = vehicule.Statut == StatutVehicule.EnAttente
                                       && vehicule.PremiereLat == null;

            if (estPremierePosition)
            {
                // Première position GPS reçue → activer le véhicule
                vehicule.Statut = StatutVehicule.Actif;
                vehicule.StatutGps = StatutGPS.PremierePositionRecue;
                vehicule.PremiereLat = positionEvent.Latitude;
                vehicule.PremiereLng = positionEvent.Longitude;
                vehicule.PremierePositionDate = positionEvent.Timestamp;
            }
            else
            {
                vehicule.StatutGps = StatutGPS.Connecte;
            }

            vehicule.DateModification = DateTime.UtcNow;
            await _vehiculeRepo.UpdateAsync(vehicule);

            // Retourner DTO pour affichage carte
            var dto = new VehiculeCarteDto
            {
                Id = vehicule.Id,
                Immatriculation = vehicule.Immatriculation,
                Marque = vehicule.Marque,
                Modele = vehicule.Modele,
                Type = vehicule.Type,
                Statut = vehicule.Statut,
                Latitude = positionEvent.Latitude,
                Longitude = positionEvent.Longitude,
                VitesseKmh = positionEvent.VitesseKmh,
                DernierePosition = positionEvent.Timestamp,
                ConducteurNom = vehicule.ConducteurNom
            };

            string message = estPremierePosition
                ? $"Première position reçue ! Véhicule '{vehicule.Immatriculation}' maintenant visible sur la carte."
                : $"Position mise à jour pour '{vehicule.Immatriculation}'.";

            return ResultatOperation<VehiculeCarteDto>.Ok(dto, message);
        }

        // --------------------------------------------------------
        // MODIFIER UN VÉHICULE
        // --------------------------------------------------------
        public async Task<ResultatOperation<Vehicule>> ModifierVehiculeAsync(
            int id, ModifierVehiculeRequest request)
        {
            var vehicule = await _vehiculeRepo.GetByIdAsync(id);
            if (vehicule == null)
                return ResultatOperation<Vehicule>.Echec($"Véhicule #{id} introuvable.");

            // Validation unicité tracker si modifié
            if (!string.IsNullOrEmpty(request.TrackerGpsId)
                && request.TrackerGpsId != vehicule.TrackerGpsId)
            {
                if (await _vehiculeRepo.ExistsTrackerGpsIdAsync(request.TrackerGpsId, id))
                    return ResultatOperation<Vehicule>.Echec(
                        $"Le tracker GPS '{request.TrackerGpsId}' est déjà assigné.");
            }

            // Appliquer les modifications (null = pas de changement)
            if (!string.IsNullOrEmpty(request.Marque)) vehicule.Marque = request.Marque.Trim();
            if (!string.IsNullOrEmpty(request.Modele)) vehicule.Modele = request.Modele.Trim();
            if (request.Annee.HasValue) vehicule.Annee = request.Annee.Value;
            if (request.Type.HasValue) vehicule.Type = request.Type.Value;
            if (!string.IsNullOrEmpty(request.TrackerGpsId)) vehicule.TrackerGpsId = request.TrackerGpsId.Trim();
            if (!string.IsNullOrEmpty(request.FournisseurGps)) vehicule.FournisseurGps = request.FournisseurGps.Trim();
            if (request.ConducteurId.HasValue) vehicule.ConducteurId = request.ConducteurId.Value;
            if (!string.IsNullOrEmpty(request.GroupeDivision)) vehicule.GroupeDivision = request.GroupeDivision.Trim();
            if (request.VitesseMaxKmh.HasValue) vehicule.VitesseMaxKmh = request.VitesseMaxKmh.Value;
            if (request.ZoneParDefautId.HasValue) vehicule.ZoneParDefautId = request.ZoneParDefautId.Value;
            if (!string.IsNullOrEmpty(request.Notes)) vehicule.Notes = request.Notes.Trim();
            if (request.Statut.HasValue) vehicule.Statut = request.Statut.Value;

            vehicule.DateModification = DateTime.UtcNow;
            await _vehiculeRepo.UpdateAsync(vehicule);

            return ResultatOperation<Vehicule>.Ok(vehicule, "Véhicule modifié avec succès.");
        }

        // --------------------------------------------------------
        // SUPPRIMER UN VÉHICULE
        // --------------------------------------------------------
        public async Task<ResultatOperation<bool>> SupprimerVehiculeAsync(int id)
        {
            var vehicule = await _vehiculeRepo.GetByIdAsync(id);
            if (vehicule == null)
                return ResultatOperation<bool>.Echec($"Véhicule #{id} introuvable.");

            if (vehicule.Statut == StatutVehicule.Actif)
                return ResultatOperation<bool>.Echec(
                    "Impossible de supprimer un véhicule actif. Passez-le en 'Inactif' d'abord.");

            await _vehiculeRepo.DeleteAsync(id);
            return ResultatOperation<bool>.Ok(true,
                $"Véhicule '{vehicule.Immatriculation}' supprimé définitivement.");
        }

        // --------------------------------------------------------
        // LISTER VÉHICULES (tous / par statut)
        // --------------------------------------------------------
        public async Task<ResultatOperation<List<Vehicule>>> ListerVehiculesAsync(
            StatutVehicule? filtre = null)
        {
            var liste = filtre.HasValue
                ? await _vehiculeRepo.GetByStatutAsync(filtre.Value)
                : await _vehiculeRepo.GetAllAsync();

            return ResultatOperation<List<Vehicule>>.Ok(liste,
                $"{liste.Count} véhicule(s) trouvé(s).");
        }

        // --------------------------------------------------------
        // VÉRIFIER UNICITÉ (pour validation formulaire temps réel)
        // --------------------------------------------------------
        public async Task<bool> VerifierUniciteImmatriculationAsync(string immatriculation, int? excludeId = null)
            => !await _vehiculeRepo.ExistsImmatriculationAsync(immatriculation, excludeId);

        public async Task<bool> VerifierUniciteTrackerAsync(string trackerGpsId, int? excludeId = null)
            => !await _vehiculeRepo.ExistsTrackerGpsIdAsync(trackerGpsId, excludeId);

        public async Task<bool> VerifierUniciteVINAsync(string vin, int? excludeId = null)
            => !await _vehiculeRepo.ExistsVINAsync(vin, excludeId);

        // --------------------------------------------------------
        // DONNÉES FORMULAIRE (dropdowns)
        // --------------------------------------------------------
        public async Task<List<(int Id, string Nom)>> GetConducteursDisponiblesAsync()
            => await _conducteurRepo.GetDisponiblesAsync();

        public async Task<List<string>> GetGroupesAsync()
            => await _groupeRepo.GetAllAsync();
    }

    // ============================================================
    // CONTRÔLEUR API REST
    // -> deplace vers Controllers/VehiculesController.cs
    // ============================================================
}
