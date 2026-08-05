using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GeoTrack.Api.Services
{
    // ============================================================
    // GEO-53 : Service de filtrage des véhicules sur la carte
    // Story parente : GEO-7 (Filtre carte)
    // Auteur : Sory Fofana
    // Date : 2026-08-05
    // ============================================================

    #region Enums

    /// <summary>
    /// Statut opérationnel d'un véhicule
    /// </summary>
    public enum StatutVehicule
    {
        Actif,
        Inactif,
        EnMaintenance,
        HorsService,
        EnAlerte
    }

    /// <summary>
    /// Type de véhicule dans la flotte
    /// </summary>
    public enum TypeVehicule
    {
        Camion,
        Voiture,
        Fourgonnette,
        Moto,
        Autobus
    }

    /// <summary>
    /// État de mouvement du véhicule
    /// </summary>
    public enum EtatMouvement
    {
        EnMouvement,
        Arrete,
        Ralenti,
        StationneDepuis30Min
    }

    /// <summary>
    /// Sévérité d'une alerte (intégration GEO-10)
    /// </summary>
    public enum SeveriteAlerte
    {
        Aucune,
        Avertissement,
        Alerte,
        Critique
    }

    /// <summary>
    /// Position relative par rapport à une zone
    /// </summary>
    public enum PositionRelativeZone
    {
        Interieur,
        Exterieur,
        Perimetre
    }

    #endregion

    #region Modèles

    /// <summary>
    /// Représente la position GPS d'un véhicule à un instant T
    /// </summary>
    public class PositionVehicule
    {
        public string VehiculeId { get; set; } = string.Empty;
        public string Immatriculation { get; set; } = string.Empty;
        public string Conducteur { get; set; } = string.Empty;
        public TypeVehicule Type { get; set; }
        public StatutVehicule Statut { get; set; }
        public EtatMouvement Mouvement { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double VitesseActuelle { get; set; } // km/h
        public DateTime DerniereMiseAJour { get; set; }
        public string GroupeFlotte { get; set; } = string.Empty;
        public string ZoneActuelle { get; set; } = string.Empty;
        public SeveriteAlerte AlerteActive { get; set; } = SeveriteAlerte.Aucune;
        public string TypeAlerte { get; set; } = string.Empty;
    }

    /// <summary>
    /// Critères de filtrage de la carte
    /// </summary>
    public class CriteresFiltrage
    {
        // --- Filtres par véhicule ---
        public List<string> VehiculeIds { get; set; } = new();
        public List<StatutVehicule> Statuts { get; set; } = new();
        public List<TypeVehicule> Types { get; set; } = new();
        public List<string> GroupesFlotte { get; set; } = new();
        public string RechercheConducteur { get; set; } = string.Empty;
        public double? VitesseMin { get; set; }
        public double? VitesseMax { get; set; }
        public List<EtatMouvement> EtatsMouvement { get; set; } = new();

        // --- Filtres par zone ---
        public List<string> ZoneIds { get; set; } = new();
        public PositionRelativeZone? PositionRelative { get; set; }
        public double? RayonRecherche { get; set; } // en mètres
        public double? CentreLatitude { get; set; }
        public double? CentreLongitude { get; set; }

        // --- Filtres par alerte (intégration GEO-10) ---
        public bool? AlertesActivesUniquement { get; set; }
        public List<SeveriteAlerte> Severites { get; set; } = new();
        public List<string> TypesAlerte { get; set; } = new();

        // --- Filtre temporel ---
        public DateTime? DerniereMiseAJourApres { get; set; }
        public int? InactifDepuisMinutes { get; set; }
    }

    /// <summary>
    /// Résultat du filtrage avec métadonnées
    /// </summary>
    public class ResultatFiltrage
    {
        public List<PositionVehicule> Vehicules { get; set; } = new();
        public int TotalVehicules { get; set; }
        public int VehiculesAffiches { get; set; }
        public int FiltresActifs { get; set; }
        public DateTime HorodatageResultat { get; set; }
        public double TempsTraitementMs { get; set; }

        // Statistiques par catégorie
        public Dictionary<StatutVehicule, int> ParStatut { get; set; } = new();
        public Dictionary<TypeVehicule, int> ParType { get; set; } = new();
        public Dictionary<SeveriteAlerte, int> ParSeverite { get; set; } = new();
    }

    /// <summary>
    /// Zone géographique (référence GEO-48)
    /// </summary>
    public class ZoneGeographiqueRef
    {
        public string Id { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public double CentreLatitude { get; set; }
        public double CentreLongitude { get; set; }
        public double RayonMetres { get; set; }
        public List<CoordonneeGps> Polygone { get; set; } = new();
    }

    public class CoordonneeGps
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    #endregion

    #region Interface Service

    /// <summary>
    /// Interface du service de filtrage carte
    /// </summary>
    public interface IFiltreCarteService
    {
        Task<ResultatFiltrage> FiltrerVehiculesAsync(CriteresFiltrage criteres);
        Task<List<PositionVehicule>> ObtenirTousVehiculesAsync();
        Task<List<ZoneGeographiqueRef>> ObtenirZonesAsync();
        Task<ResultatFiltrage> FiltrerParProximiteAsync(double latitude, double longitude, double rayonMetres);
        int CompterFiltresActifs(CriteresFiltrage criteres);
    }

    #endregion

    #region Implémentation Service

    /// <summary>
    /// Service principal de filtrage des véhicules sur la carte
    /// Logique : OR intra-catégorie, AND inter-catégories
    /// Contraintes : max 500 véhicules, timeout 3s
    /// </summary>
    public class FiltreCarteService : IFiltreCarteService
    {
        private const int MAX_VEHICULES_AFFICHAGE = 500;
        private const int TIMEOUT_MS = 3000;
        private const double RAYON_TERRE_KM = 6371.0;

        private readonly IVehiculeRepository _vehiculeRepository;
        private readonly IZoneRepository _zoneRepository;

        public FiltreCarteService(
            IVehiculeRepository vehiculeRepository,
            IZoneRepository zoneRepository)
        {
            _vehiculeRepository = vehiculeRepository;
            _zoneRepository = zoneRepository;
        }

        /// <summary>
        /// Filtre les véhicules selon les critères combinés
        /// Logique : OR intra-catégorie, AND inter-catégories
        /// </summary>
        public async Task<ResultatFiltrage> FiltrerVehiculesAsync(CriteresFiltrage criteres)
        {
            var chrono = System.Diagnostics.Stopwatch.StartNew();

            // 1. Récupérer tous les véhicules
            var tousVehicules = await _vehiculeRepository.ObtenirPositionsAsync();
            var totalVehicules = tousVehicules.Count;

            // 2. Appliquer les filtres (AND entre catégories)
            var resultat = tousVehicules.AsEnumerable();

            // Filtre véhicule (OR intra-catégorie)
            resultat = AppliquerFiltreVehicule(resultat, criteres);

            // Filtre zone (OR intra-catégorie)
            resultat = await AppliquerFiltreZoneAsync(resultat, criteres);

            // Filtre alerte (OR intra-catégorie)
            resultat = AppliquerFiltreAlerte(resultat, criteres);

            // Filtre temporel
            resultat = AppliquerFiltreTemporel(resultat, criteres);

            // 3. Limiter à MAX_VEHICULES_AFFICHAGE
            var vehiculesFiltres = resultat.Take(MAX_VEHICULES_AFFICHAGE).ToList();

            chrono.Stop();

            // 4. Construire le résultat avec statistiques
            return new ResultatFiltrage
            {
                Vehicules = vehiculesFiltres,
                TotalVehicules = totalVehicules,
                VehiculesAffiches = vehiculesFiltres.Count,
                FiltresActifs = CompterFiltresActifs(criteres),
                HorodatageResultat = DateTime.UtcNow,
                TempsTraitementMs = chrono.Elapsed.TotalMilliseconds,
                ParStatut = vehiculesFiltres
                    .GroupBy(v => v.Statut)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ParType = vehiculesFiltres
                    .GroupBy(v => v.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ParSeverite = vehiculesFiltres
                    .Where(v => v.AlerteActive != SeveriteAlerte.Aucune)
                    .GroupBy(v => v.AlerteActive)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        /// <summary>
        /// Retourne tous les véhicules sans filtre
        /// </summary>
        public async Task<List<PositionVehicule>> ObtenirTousVehiculesAsync()
        {
            return await _vehiculeRepository.ObtenirPositionsAsync();
        }

        /// <summary>
        /// Retourne toutes les zones géographiques disponibles
        /// </summary>
        public async Task<List<ZoneGeographiqueRef>> ObtenirZonesAsync()
        {
            return await _zoneRepository.ObtenirZonesAsync();
        }

        /// <summary>
        /// Filtre par proximité (rayon autour d'un point GPS)
        /// </summary>
        public async Task<ResultatFiltrage> FiltrerParProximiteAsync(
            double latitude, double longitude, double rayonMetres)
        {
            var criteres = new CriteresFiltrage
            {
                CentreLatitude = latitude,
                CentreLongitude = longitude,
                RayonRecherche = rayonMetres
            };
            return await FiltrerVehiculesAsync(criteres);
        }

        /// <summary>
        /// Compte le nombre de filtres actifs pour l'UI
        /// </summary>
        public int CompterFiltresActifs(CriteresFiltrage criteres)
        {
            int count = 0;

            if (criteres.VehiculeIds.Any()) count++;
            if (criteres.Statuts.Any()) count++;
            if (criteres.Types.Any()) count++;
            if (criteres.GroupesFlotte.Any()) count++;
            if (!string.IsNullOrWhiteSpace(criteres.RechercheConducteur)) count++;
            if (criteres.VitesseMin.HasValue || criteres.VitesseMax.HasValue) count++;
            if (criteres.EtatsMouvement.Any()) count++;
            if (criteres.ZoneIds.Any()) count++;
            if (criteres.PositionRelative.HasValue) count++;
            if (criteres.RayonRecherche.HasValue) count++;
            if (criteres.AlertesActivesUniquement == true) count++;
            if (criteres.Severites.Any()) count++;
            if (criteres.TypesAlerte.Any()) count++;
            if (criteres.DerniereMiseAJourApres.HasValue) count++;
            if (criteres.InactifDepuisMinutes.HasValue) count++;

            return count;
        }

        #region Filtres privés

        /// <summary>
        /// Applique les filtres véhicule (OR entre critères de même catégorie)
        /// </summary>
        private IEnumerable<PositionVehicule> AppliquerFiltreVehicule(
            IEnumerable<PositionVehicule> vehicules, CriteresFiltrage criteres)
        {
            // Filtre par IDs spécifiques
            if (criteres.VehiculeIds.Any())
            {
                vehicules = vehicules.Where(v =>
                    criteres.VehiculeIds.Contains(v.VehiculeId));
            }

            // Filtre par statut (OR)
            if (criteres.Statuts.Any())
            {
                vehicules = vehicules.Where(v =>
                    criteres.Statuts.Contains(v.Statut));
            }

            // Filtre par type (OR)
            if (criteres.Types.Any())
            {
                vehicules = vehicules.Where(v =>
                    criteres.Types.Contains(v.Type));
            }

            // Filtre par groupe de flotte (OR)
            if (criteres.GroupesFlotte.Any())
            {
                vehicules = vehicules.Where(v =>
                    criteres.GroupesFlotte.Contains(v.GroupeFlotte));
            }

            // Recherche conducteur (contient, insensible casse)
            if (!string.IsNullOrWhiteSpace(criteres.RechercheConducteur))
            {
                vehicules = vehicules.Where(v =>
                    v.Conducteur.Contains(criteres.RechercheConducteur,
                        StringComparison.OrdinalIgnoreCase));
            }

            // Filtre par plage de vitesse
            if (criteres.VitesseMin.HasValue)
            {
                vehicules = vehicules.Where(v =>
                    v.VitesseActuelle >= criteres.VitesseMin.Value);
            }
            if (criteres.VitesseMax.HasValue)
            {
                vehicules = vehicules.Where(v =>
                    v.VitesseActuelle <= criteres.VitesseMax.Value);
            }

            // Filtre par état de mouvement (OR)
            if (criteres.EtatsMouvement.Any())
            {
                vehicules = vehicules.Where(v =>
                    criteres.EtatsMouvement.Contains(v.Mouvement));
            }

            return vehicules;
        }

        /// <summary>
        /// Applique les filtres zone géographique
        /// </summary>
        private async Task<IEnumerable<PositionVehicule>> AppliquerFiltreZoneAsync(
            IEnumerable<PositionVehicule> vehicules, CriteresFiltrage criteres)
        {
            // Filtre par zones spécifiques (OR)
            if (criteres.ZoneIds.Any())
            {
                vehicules = vehicules.Where(v =>
                    criteres.ZoneIds.Contains(v.ZoneActuelle));
            }

            // Filtre par rayon personnalisé
            if (criteres.RayonRecherche.HasValue &&
                criteres.CentreLatitude.HasValue &&
                criteres.CentreLongitude.HasValue)
            {
                var centre = new CoordonneeGps
                {
                    Latitude = criteres.CentreLatitude.Value,
                    Longitude = criteres.CentreLongitude.Value
                };
                var rayonKm = criteres.RayonRecherche.Value / 1000.0;

                vehicules = vehicules.Where(v =>
                    CalculerDistanceHaversine(
                        centre.Latitude, centre.Longitude,
                        v.Latitude, v.Longitude) <= rayonKm);
            }

            // Filtre par position relative
            if (criteres.PositionRelative.HasValue && criteres.ZoneIds.Any())
            {
                var zones = await _zoneRepository.ObtenirZonesAsync();
                var zonesSelectionnees = zones
                    .Where(z => criteres.ZoneIds.Contains(z.Id))
                    .ToList();

                vehicules = criteres.PositionRelative.Value switch
                {
                    PositionRelativeZone.Interieur => vehicules.Where(v =>
                        EstDansUneZone(v, zonesSelectionnees)),
                    PositionRelativeZone.Exterieur => vehicules.Where(v =>
                        !EstDansUneZone(v, zonesSelectionnees)),
                    PositionRelativeZone.Perimetre => vehicules.Where(v =>
                        EstSurPerimetre(v, zonesSelectionnees)),
                    _ => vehicules
                };
            }

            return vehicules;
        }

        /// <summary>
        /// Applique les filtres alerte (intégration GEO-10)
        /// </summary>
        private IEnumerable<PositionVehicule> AppliquerFiltreAlerte(
            IEnumerable<PositionVehicule> vehicules, CriteresFiltrage criteres)
        {
            // Uniquement véhicules avec alerte active
            if (criteres.AlertesActivesUniquement == true)
            {
                vehicules = vehicules.Where(v =>
                    v.AlerteActive != SeveriteAlerte.Aucune);
            }

            // Filtre par sévérité (OR)
            if (criteres.Severites.Any())
            {
                vehicules = vehicules.Where(v =>
                    criteres.Severites.Contains(v.AlerteActive));
            }

            // Filtre par type d'alerte (OR)
            if (criteres.TypesAlerte.Any())
            {
                vehicules = vehicules.Where(v =>
                    criteres.TypesAlerte.Contains(v.TypeAlerte));
            }

            return vehicules;
        }

        /// <summary>
        /// Applique les filtres temporels
        /// </summary>
        private IEnumerable<PositionVehicule> AppliquerFiltreTemporel(
            IEnumerable<PositionVehicule> vehicules, CriteresFiltrage criteres)
        {
            // Mise à jour après une date donnée
            if (criteres.DerniereMiseAJourApres.HasValue)
            {
                vehicules = vehicules.Where(v =>
                    v.DerniereMiseAJour >= criteres.DerniereMiseAJourApres.Value);
            }

            // Véhicules inactifs depuis X minutes
            if (criteres.InactifDepuisMinutes.HasValue)
            {
                var seuil = DateTime.UtcNow.AddMinutes(-criteres.InactifDepuisMinutes.Value);
                vehicules = vehicules.Where(v =>
                    v.DerniereMiseAJour <= seuil);
            }

            return vehicules;
        }

        #endregion

        #region Utilitaires géographiques

        /// <summary>
        /// Calcule la distance entre deux points GPS (formule de Haversine)
        /// </summary>
        private double CalculerDistanceHaversine(
            double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = DegreVersRadian(lat2 - lat1);
            var dLon = DegreVersRadian(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreVersRadian(lat1)) *
                    Math.Cos(DegreVersRadian(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return RAYON_TERRE_KM * c;
        }

        private double DegreVersRadian(double degre) => degre * Math.PI / 180.0;

        /// <summary>
        /// Vérifie si un véhicule est dans l'une des zones (rayon circulaire)
        /// </summary>
        private bool EstDansUneZone(PositionVehicule vehicule, List<ZoneGeographiqueRef> zones)
        {
            return zones.Any(zone =>
            {
                var distance = CalculerDistanceHaversine(
                    vehicule.Latitude, vehicule.Longitude,
                    zone.CentreLatitude, zone.CentreLongitude);
                return distance <= (zone.RayonMetres / 1000.0);
            });
        }

        /// <summary>
        /// Vérifie si un véhicule est sur le périmètre d'une zone (±50m)
        /// </summary>
        private bool EstSurPerimetre(PositionVehicule vehicule, List<ZoneGeographiqueRef> zones)
        {
            const double TOLERANCE_PERIMETRE_KM = 0.05; // 50 mètres

            return zones.Any(zone =>
            {
                var distance = CalculerDistanceHaversine(
                    vehicule.Latitude, vehicule.Longitude,
                    zone.CentreLatitude, zone.CentreLongitude);
                var rayonKm = zone.RayonMetres / 1000.0;
                return Math.Abs(distance - rayonKm) <= TOLERANCE_PERIMETRE_KM;
            });
        }

        #endregion
    }

    #endregion

    #region Interfaces Repository

    /// <summary>
    /// Interface d'accès aux données véhicules
    /// </summary>
    public interface IVehiculeRepository
    {
        Task<List<PositionVehicule>> ObtenirPositionsAsync();
        Task<PositionVehicule?> ObtenirParIdAsync(string vehiculeId);
    }

    /// <summary>
    /// Interface d'accès aux données zones géographiques
    /// </summary>
    public interface IZoneRepository
    {
        Task<List<ZoneGeographiqueRef>> ObtenirZonesAsync();
        Task<ZoneGeographiqueRef?> ObtenirParIdAsync(string zoneId);
    }

    #endregion

    #region Controller API

    /// <summary>
    /// Contrôleur API REST pour le filtrage carte
    /// 6 endpoints définis dans GEO-39
    /// </summary>
    // [ApiController]
    // [Route("api/[controller]")]
    public class FiltreCarteController
    {
        private readonly IFiltreCarteService _filtreService;

        public FiltreCarteController(IFiltreCarteService filtreService)
        {
            _filtreService = filtreService;
        }

        /// <summary>
        /// GET /api/filtre-carte/vehicules
        /// Retourne tous les véhicules (sans filtre)
        /// </summary>
        public async Task<List<PositionVehicule>> GetTousVehicules()
        {
            return await _filtreService.ObtenirTousVehiculesAsync();
        }

        /// <summary>
        /// POST /api/filtre-carte/filtrer
        /// Filtre les véhicules selon les critères
        /// </summary>
        public async Task<ResultatFiltrage> PostFiltrer(CriteresFiltrage criteres)
        {
            return await _filtreService.FiltrerVehiculesAsync(criteres);
        }

        /// <summary>
        /// GET /api/filtre-carte/zones
        /// Retourne toutes les zones disponibles
        /// </summary>
        public async Task<List<ZoneGeographiqueRef>> GetZones()
        {
            return await _filtreService.ObtenirZonesAsync();
        }

        /// <summary>
        /// GET /api/filtre-carte/proximite?lat={lat}&lon={lon}&rayon={m}
        /// Filtre par proximité
        /// </summary>
        public async Task<ResultatFiltrage> GetProximite(
            double lat, double lon, double rayon)
        {
            return await _filtreService.FiltrerParProximiteAsync(lat, lon, rayon);
        }

        /// <summary>
        /// GET /api/filtre-carte/statistiques
        /// Retourne les statistiques sans véhicules
        /// </summary>
        public async Task<ResultatFiltrage> GetStatistiques()
        {
            var criteres = new CriteresFiltrage();
            return await _filtreService.FiltrerVehiculesAsync(criteres);
        }

        /// <summary>
        /// GET /api/filtre-carte/compteur-filtres
        /// Retourne le nombre de filtres actifs
        /// </summary>
        public int GetCompteurFiltres(CriteresFiltrage criteres)
        {
            return _filtreService.CompterFiltresActifs(criteres);
        }
    }

    #endregion
}
