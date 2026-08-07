// ============================================================
// GEO-12: Service Historique Trajets
// Story: En tant que gestionnaire de flotte, je souhaite consulter
//        l'historique complet des trajets d'un véhicule afin
//        d'analyser son utilisation.
// Critères:
//   1. L'historique affiche les trajets avec date et durée
//   2. Le trajet peut être visualisé sur une carte
// Epic: GEO-3 — Historique et tableau de bord analytique
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace GeoTrack.Api.Services.HistoriqueTrajets
{
    // ============================================================
    // ENUMS
    // ============================================================

    /// <summary>Statut d'un trajet</summary>
    public enum StatutTrajet
    {
        EnCours,
        Termine,
        Interrompu
    }

    /// <summary>Type d'arrêt pendant un trajet</summary>
    public enum TypeArret
    {
        Pause,
        Livraison,
        Ravitaillement,
        Incident,
        Autre
    }

    // ============================================================
    // MODÈLES
    // ============================================================

    /// <summary>Point GPS enregistré pendant un trajet</summary>
    public class PointGps
    {
        public int Id { get; set; }
        public int TrajetId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double VitesseKmh { get; set; }
        public double Altitude { get; set; }
        public int Cap { get; set; } // 0-360 degrés
        public DateTime Horodatage { get; set; }
        public int Precision { get; set; } // mètres
        public int OrdreSequence { get; set; }
    }

    /// <summary>Arrêt détecté pendant un trajet</summary>
    public class ArretTrajet
    {
        public int Id { get; set; }
        public int TrajetId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime HeureDebut { get; set; }
        public DateTime HeureFin { get; set; }
        public TimeSpan Duree => HeureFin - HeureDebut;
        public TypeArret Type { get; set; }
        public string Adresse { get; set; }
    }

    /// <summary>Entité principale Trajet</summary>
    public class Trajet
    {
        public int Id { get; set; }
        public int VehiculeId { get; set; }
        public string NomVehicule { get; set; }
        public string Immatriculation { get; set; }

        // --- Critère #1 : Date et durée ---
        public DateTime DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public TimeSpan Duree => (DateFin ?? DateTime.UtcNow) - DateDebut;

        // --- Métriques ---
        public double DistanceKm { get; set; }
        public double VitesseMoyenneKmh { get; set; }
        public double VitesseMaxKmh { get; set; }
        public double ConsommationLitres { get; set; }
        public int NombreArrets { get; set; }
        public TimeSpan TempsArretTotal { get; set; }

        // --- Localisation ---
        public string AdresseDepart { get; set; }
        public string AdresseArrivee { get; set; }
        public double LatitudeDepart { get; set; }
        public double LongitudeDepart { get; set; }
        public double LatitudeArrivee { get; set; }
        public double LongitudeArrivee { get; set; }

        // --- Statut ---
        public StatutTrajet Statut { get; set; }
        public string ConducteurNom { get; set; }

        // --- Points GPS pour carte (Critère #2) ---
        public List<PointGps> PointsGps { get; set; } = new();
        public List<ArretTrajet> Arrets { get; set; } = new();
    }

    // ============================================================
    // DTOs REQUÊTES
    // ============================================================

    /// <summary>Paramètres de recherche historique trajets</summary>
    public class RechercheTrajetRequest
    {
        public int VehiculeId { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public StatutTrajet? Statut { get; set; }
        public double? DistanceMinKm { get; set; }
        public double? DistanceMaxKm { get; set; }
        public string TriPar { get; set; } = "DateDebut"; // DateDebut, Duree, Distance
        public bool TriDescendant { get; set; } = true;
        public int Page { get; set; } = 1;
        public int ParPage { get; set; } = 5;
    }

    // ============================================================
    // DTOs RÉPONSES
    // ============================================================

    /// <summary>Trajet résumé pour la liste (Critère #1)</summary>
    public class TrajetResumeDto
    {
        public int Id { get; set; }
        public string NomVehicule { get; set; }
        public string Immatriculation { get; set; }
        public DateTime DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public string DureeFormatee { get; set; } // "2h 35min"
        public double DistanceKm { get; set; }
        public double VitesseMoyenneKmh { get; set; }
        public int NombreArrets { get; set; }
        public string AdresseDepart { get; set; }
        public string AdresseArrivee { get; set; }
        public StatutTrajet Statut { get; set; }
        public string ConducteurNom { get; set; }
    }

    /// <summary>Détail trajet avec points GPS pour carte (Critère #2)</summary>
    public class TrajetDetailCarteDto
    {
        public int Id { get; set; }
        public string NomVehicule { get; set; }
        public DateTime DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public string DureeFormatee { get; set; }
        public double DistanceKm { get; set; }
        public double VitesseMoyenneKmh { get; set; }
        public double VitesseMaxKmh { get; set; }
        public double ConsommationLitres { get; set; }
        public int NombreArrets { get; set; }
        public TimeSpan TempsArretTotal { get; set; }
        public string AdresseDepart { get; set; }
        public string AdresseArrivee { get; set; }
        public StatutTrajet Statut { get; set; }

        // Points GPS pour tracer le trajet sur la carte
        public List<PointGpsCarteDto> PointsCarte { get; set; } = new();
        public List<ArretCarteDto> ArretsCarte { get; set; } = new();

        // Bounding box pour centrer la carte
        public double LatMin { get; set; }
        public double LatMax { get; set; }
        public double LngMin { get; set; }
        public double LngMax { get; set; }
    }

    /// <summary>Point GPS simplifié pour affichage carte</summary>
    public class PointGpsCarteDto
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double VitesseKmh { get; set; }
        public DateTime Horodatage { get; set; }
        public string CouleurGradient { get; set; } // #00C853, #FFD600, #FF6D00, #D50000
    }

    /// <summary>Arrêt simplifié pour marqueur carte</summary>
    public class ArretCarteDto
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string DureeFormatee { get; set; }
        public TypeArret Type { get; set; }
        public string Adresse { get; set; }
    }

    /// <summary>Statistiques véhicule sur une période</summary>
    public class StatistiquesVehiculeDto
    {
        public int VehiculeId { get; set; }
        public string NomVehicule { get; set; }
        public DateTime PeriodeDebut { get; set; }
        public DateTime PeriodeFin { get; set; }
        public int NombreTrajets { get; set; }
        public double DistanceTotaleKm { get; set; }
        public TimeSpan DureeTotale { get; set; }
        public double VitesseMoyenneKmh { get; set; }
        public double VitesseMaxKmh { get; set; }
        public double ConsommationTotaleLitres { get; set; }
        public double ConsommationMoyennePour100Km { get; set; }
        public int NombreArrets { get; set; }
        public TimeSpan TempsArretTotal { get; set; }
        public double PourcentageTempsEnMouvement { get; set; }
        public TrajetResumeDto TrajetPlusLong { get; set; }
        public TrajetResumeDto TrajetPlusCourt { get; set; }
    }

    /// <summary>Résultat paginé</summary>
    public class ResultatPagine<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int ParPage { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / ParPage);
        public bool APageSuivante => Page < TotalPages;
        public bool APagePrecedente => Page > 1;
    }

    /// <summary>Ligne export CSV</summary>
    public class TrajetExportCsvDto
    {
        public int TrajetId { get; set; }
        public string Vehicule { get; set; }
        public string Immatriculation { get; set; }
        public string DateDebut { get; set; }
        public string DateFin { get; set; }
        public string Duree { get; set; }
        public double DistanceKm { get; set; }
        public double VitesseMoyKmh { get; set; }
        public double VitesseMaxKmh { get; set; }
        public int Arrets { get; set; }
        public string Depart { get; set; }
        public string Arrivee { get; set; }
        public string Statut { get; set; }
        public string Conducteur { get; set; }
    }

    // ============================================================
    // SERVICE — LOGIQUE MÉTIER
    // ============================================================

    public class HistoriqueTrajetService
    {
        // Données simulées
        private static readonly List<Trajet> _trajets = GenererDonneesSimulees();

        // --- Constantes métier ---
        private const int MAX_POINTS_GPS = 10000;
        private const int RETENTION_MOIS = 12;
        private const double DUREE_MIN_TRAJET_MINUTES = 2.0;
        private const int SEUIL_ARRET_SECONDES = 120; // 2 min sans mouvement = arrêt

        // ==========================================================
        // CRITÈRE #1 : Historique avec date et durée
        // ==========================================================

        /// <summary>Lister les trajets d'un véhicule avec pagination et filtres</summary>
        public ResultatPagine<TrajetResumeDto> ListerTrajets(RechercheTrajetRequest request)
        {
            var query = _trajets.Where(t => t.VehiculeId == request.VehiculeId);

            // Filtres
            if (request.DateDebut.HasValue)
                query = query.Where(t => t.DateDebut >= request.DateDebut.Value);

            if (request.DateFin.HasValue)
                query = query.Where(t => t.DateDebut <= request.DateFin.Value);

            if (request.Statut.HasValue)
                query = query.Where(t => t.Statut == request.Statut.Value);

            if (request.DistanceMinKm.HasValue)
                query = query.Where(t => t.DistanceKm >= request.DistanceMinKm.Value);

            if (request.DistanceMaxKm.HasValue)
                query = query.Where(t => t.DistanceKm <= request.DistanceMaxKm.Value);

            // Rétention 12 mois
            var dateMinRetention = DateTime.UtcNow.AddMonths(-RETENTION_MOIS);
            query = query.Where(t => t.DateDebut >= dateMinRetention);

            // Tri
            query = request.TriPar switch
            {
                "Duree" => request.TriDescendant
                    ? query.OrderByDescending(t => t.Duree)
                    : query.OrderBy(t => t.Duree),
                "Distance" => request.TriDescendant
                    ? query.OrderByDescending(t => t.DistanceKm)
                    : query.OrderBy(t => t.DistanceKm),
                _ => request.TriDescendant
                    ? query.OrderByDescending(t => t.DateDebut)
                    : query.OrderBy(t => t.DateDebut)
            };

            var totalItems = query.Count();
            var items = query
                .Skip((request.Page - 1) * request.ParPage)
                .Take(request.ParPage)
                .Select(MapToResume)
                .ToList();

            return new ResultatPagine<TrajetResumeDto>
            {
                Items = items,
                Page = request.Page,
                ParPage = request.ParPage,
                TotalItems = totalItems
            };
        }

        // ==========================================================
        // CRITÈRE #2 : Visualisation sur carte
        // ==========================================================

        /// <summary>Obtenir le détail d'un trajet avec tous les points GPS pour la carte</summary>
        public TrajetDetailCarteDto ObtenirTrajetPourCarte(int trajetId)
        {
            var trajet = _trajets.FirstOrDefault(t => t.Id == trajetId);
            if (trajet == null) return null;

            // Simplification Douglas-Peucker si > MAX_POINTS_GPS
            var pointsOptimises = trajet.PointsGps.Count > MAX_POINTS_GPS
                ? SimplifierDouglasPeucker(trajet.PointsGps, 0.0001)
                : trajet.PointsGps;

            var pointsCarte = pointsOptimises.Select(p => new PointGpsCarteDto
            {
                Lat = p.Latitude,
                Lng = p.Longitude,
                VitesseKmh = p.VitesseKmh,
                Horodatage = p.Horodatage,
                CouleurGradient = ObtenirCouleurVitesse(p.VitesseKmh)
            }).ToList();

            var arretsCarte = trajet.Arrets.Select(a => new ArretCarteDto
            {
                Lat = a.Latitude,
                Lng = a.Longitude,
                DureeFormatee = FormaterDuree(a.Duree),
                Type = a.Type,
                Adresse = a.Adresse
            }).ToList();

            return new TrajetDetailCarteDto
            {
                Id = trajet.Id,
                NomVehicule = trajet.NomVehicule,
                DateDebut = trajet.DateDebut,
                DateFin = trajet.DateFin,
                DureeFormatee = FormaterDuree(trajet.Duree),
                DistanceKm = trajet.DistanceKm,
                VitesseMoyenneKmh = trajet.VitesseMoyenneKmh,
                VitesseMaxKmh = trajet.VitesseMaxKmh,
                ConsommationLitres = trajet.ConsommationLitres,
                NombreArrets = trajet.NombreArrets,
                TempsArretTotal = trajet.TempsArretTotal,
                AdresseDepart = trajet.AdresseDepart,
                AdresseArrivee = trajet.AdresseArrivee,
                Statut = trajet.Statut,
                PointsCarte = pointsCarte,
                ArretsCarte = arretsCarte,
                // Bounding box
                LatMin = pointsCarte.Any() ? pointsCarte.Min(p => p.Lat) : 0,
                LatMax = pointsCarte.Any() ? pointsCarte.Max(p => p.Lat) : 0,
                LngMin = pointsCarte.Any() ? pointsCarte.Min(p => p.Lng) : 0,
                LngMax = pointsCarte.Any() ? pointsCarte.Max(p => p.Lng) : 0
            };
        }

        // ==========================================================
        // STATISTIQUES
        // ==========================================================

        /// <summary>Statistiques d'utilisation véhicule sur une période</summary>
        public StatistiquesVehiculeDto ObtenirStatistiques(int vehiculeId, DateTime debut, DateTime fin)
        {
            var trajets = _trajets
                .Where(t => t.VehiculeId == vehiculeId
                    && t.DateDebut >= debut
                    && t.DateDebut <= fin
                    && t.Statut == StatutTrajet.Termine)
                .ToList();

            if (!trajets.Any())
                return new StatistiquesVehiculeDto
                {
                    VehiculeId = vehiculeId,
                    PeriodeDebut = debut,
                    PeriodeFin = fin,
                    NombreTrajets = 0
                };

            var dureeTotale = TimeSpan.FromTicks(trajets.Sum(t => t.Duree.Ticks));
            var tempsArretTotal = TimeSpan.FromTicks(trajets.Sum(t => t.TempsArretTotal.Ticks));
            var distanceTotale = trajets.Sum(t => t.DistanceKm);
            var consoTotale = trajets.Sum(t => t.ConsommationLitres);

            return new StatistiquesVehiculeDto
            {
                VehiculeId = vehiculeId,
                NomVehicule = trajets.First().NomVehicule,
                PeriodeDebut = debut,
                PeriodeFin = fin,
                NombreTrajets = trajets.Count,
                DistanceTotaleKm = Math.Round(distanceTotale, 1),
                DureeTotale = dureeTotale,
                VitesseMoyenneKmh = Math.Round(trajets.Average(t => t.VitesseMoyenneKmh), 1),
                VitesseMaxKmh = trajets.Max(t => t.VitesseMaxKmh),
                ConsommationTotaleLitres = Math.Round(consoTotale, 1),
                ConsommationMoyennePour100Km = distanceTotale > 0
                    ? Math.Round(consoTotale / distanceTotale * 100, 1)
                    : 0,
                NombreArrets = trajets.Sum(t => t.NombreArrets),
                TempsArretTotal = tempsArretTotal,
                PourcentageTempsEnMouvement = dureeTotale.TotalMinutes > 0
                    ? Math.Round((1 - tempsArretTotal.TotalMinutes / dureeTotale.TotalMinutes) * 100, 1)
                    : 0,
                TrajetPlusLong = MapToResume(trajets.OrderByDescending(t => t.DistanceKm).First()),
                TrajetPlusCourt = MapToResume(trajets.OrderBy(t => t.DistanceKm).First())
            };
        }

        // ==========================================================
        // EXPORT CSV
        // ==========================================================

        /// <summary>Exporter les trajets en format CSV</summary>
        public string ExporterCsv(int vehiculeId, DateTime? debut, DateTime? fin)
        {
            var query = _trajets.Where(t => t.VehiculeId == vehiculeId);

            if (debut.HasValue) query = query.Where(t => t.DateDebut >= debut.Value);
            if (fin.HasValue) query = query.Where(t => t.DateDebut <= fin.Value);

            var trajets = query.OrderByDescending(t => t.DateDebut).ToList();

            var lignes = new List<string>
            {
                "TrajetId,Vehicule,Immatriculation,DateDebut,DateFin,Duree,DistanceKm,VitesseMoyKmh,VitesseMaxKmh,Arrets,Depart,Arrivee,Statut,Conducteur"
            };

            foreach (var t in trajets)
            {
                lignes.Add($"{t.Id},{t.NomVehicule},{t.Immatriculation}," +
                    $"{t.DateDebut:yyyy-MM-dd HH:mm},{t.DateFin:yyyy-MM-dd HH:mm}," +
                    $"{FormaterDuree(t.Duree)},{t.DistanceKm:F1},{t.VitesseMoyenneKmh:F1}," +
                    $"{t.VitesseMaxKmh:F1},{t.NombreArrets},{t.AdresseDepart}," +
                    $"{t.AdresseArrivee},{t.Statut},{t.ConducteurNom}");
            }

            return string.Join("\n", lignes);
        }

        // ==========================================================
        // DÉTECTION AUTOMATIQUE TRAJETS
        // ==========================================================

        /// <summary>Détecter début/fin de trajet à partir de positions GPS brutes</summary>
        public Trajet DetecterTrajet(List<PointGps> positionsBrutes, int vehiculeId)
        {
            if (positionsBrutes == null || positionsBrutes.Count < 2)
                return null;

            var ordonnees = positionsBrutes.OrderBy(p => p.Horodatage).ToList();

            // Trouver le premier mouvement (vitesse > 0)
            var debut = ordonnees.FirstOrDefault(p => p.VitesseKmh > 0);
            if (debut == null) return null;

            // Trouver les arrêts (> SEUIL_ARRET_SECONDES sans mouvement)
            var arrets = new List<ArretTrajet>();
            int indexDebutArret = -1;

            for (int i = 1; i < ordonnees.Count; i++)
            {
                if (ordonnees[i].VitesseKmh == 0)
                {
                    if (indexDebutArret == -1)
                        indexDebutArret = i;
                }
                else
                {
                    if (indexDebutArret != -1)
                    {
                        var dureeArret = ordonnees[i].Horodatage - ordonnees[indexDebutArret].Horodatage;
                        if (dureeArret.TotalSeconds >= SEUIL_ARRET_SECONDES)
                        {
                            arrets.Add(new ArretTrajet
                            {
                                TrajetId = 0,
                                Latitude = ordonnees[indexDebutArret].Latitude,
                                Longitude = ordonnees[indexDebutArret].Longitude,
                                HeureDebut = ordonnees[indexDebutArret].Horodatage,
                                HeureFin = ordonnees[i - 1].Horodatage,
                                Type = TypeArret.Autre
                            });
                        }
                        indexDebutArret = -1;
                    }
                }
            }

            // Calculer distance totale (Haversine)
            double distanceTotale = 0;
            double vitesseMax = 0;

            for (int i = 1; i < ordonnees.Count; i++)
            {
                distanceTotale += CalculerDistanceHaversine(
                    ordonnees[i - 1].Latitude, ordonnees[i - 1].Longitude,
                    ordonnees[i].Latitude, ordonnees[i].Longitude);

                if (ordonnees[i].VitesseKmh > vitesseMax)
                    vitesseMax = ordonnees[i].VitesseKmh;
            }

            var dureeTrajet = ordonnees.Last().Horodatage - debut.Horodatage;

            // Vérifier durée minimale
            if (dureeTrajet.TotalMinutes < DUREE_MIN_TRAJET_MINUTES)
                return null;

            var tempsArretTotal = TimeSpan.FromTicks(arrets.Sum(a => a.Duree.Ticks));

            return new Trajet
            {
                VehiculeId = vehiculeId,
                DateDebut = debut.Horodatage,
                DateFin = ordonnees.Last().Horodatage,
                DistanceKm = Math.Round(distanceTotale, 2),
                VitesseMoyenneKmh = dureeTrajet.TotalHours > 0
                    ? Math.Round(distanceTotale / dureeTrajet.TotalHours, 1)
                    : 0,
                VitesseMaxKmh = vitesseMax,
                NombreArrets = arrets.Count,
                TempsArretTotal = tempsArretTotal,
                LatitudeDepart = debut.Latitude,
                LongitudeDepart = debut.Longitude,
                LatitudeArrivee = ordonnees.Last().Latitude,
                LongitudeArrivee = ordonnees.Last().Longitude,
                Statut = StatutTrajet.Termine,
                PointsGps = ordonnees,
                Arrets = arrets
            };
        }

        // ==========================================================
        // MÉTHODES UTILITAIRES
        // ==========================================================

        /// <summary>Couleur gradient selon vitesse (pour polyline carte)</summary>
        private string ObtenirCouleurVitesse(double vitesseKmh)
        {
            return vitesseKmh switch
            {
                <= 30 => "#00C853",   // Vert — lent
                <= 60 => "#FFD600",   // Jaune — modéré
                <= 90 => "#FF6D00",   // Orange — rapide
                _ => "#D50000"        // Rouge — très rapide
            };
        }

        /// <summary>Formater une durée en texte lisible</summary>
        private string FormaterDuree(TimeSpan duree)
        {
            if (duree.TotalDays >= 1)
                return $"{(int)duree.TotalDays}j {duree.Hours}h {duree.Minutes}min";
            if (duree.TotalHours >= 1)
                return $"{(int)duree.TotalHours}h {duree.Minutes}min";
            return $"{duree.Minutes}min";
        }

        /// <summary>Distance Haversine entre 2 coordonnées GPS (en km)</summary>
        private double CalculerDistanceHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0; // Rayon Terre en km

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        /// <summary>Simplification Douglas-Peucker pour réduire les points GPS</summary>
        private List<PointGps> SimplifierDouglasPeucker(List<PointGps> points, double epsilon)
        {
            if (points.Count < 3) return points;

            // Trouver le point le plus éloigné de la ligne début-fin
            double distMax = 0;
            int indexMax = 0;

            var debut = points.First();
            var fin = points.Last();

            for (int i = 1; i < points.Count - 1; i++)
            {
                double dist = DistancePointLigne(points[i], debut, fin);
                if (dist > distMax)
                {
                    distMax = dist;
                    indexMax = i;
                }
            }

            // Si distance max > epsilon, simplifier récursivement
            if (distMax > epsilon)
            {
                var gauche = SimplifierDouglasPeucker(points.Take(indexMax + 1).ToList(), epsilon);
                var droite = SimplifierDouglasPeucker(points.Skip(indexMax).ToList(), epsilon);

                return gauche.Take(gauche.Count - 1).Concat(droite).ToList();
            }

            return new List<PointGps> { debut, fin };
        }

        /// <summary>Distance d'un point à une ligne (pour Douglas-Peucker)</summary>
        private double DistancePointLigne(PointGps point, PointGps ligneDebut, PointGps ligneFin)
        {
            double dx = ligneFin.Latitude - ligneDebut.Latitude;
            double dy = ligneFin.Longitude - ligneDebut.Longitude;

            if (dx == 0 && dy == 0)
                return CalculerDistanceHaversine(point.Latitude, point.Longitude,
                    ligneDebut.Latitude, ligneDebut.Longitude);

            double t = ((point.Latitude - ligneDebut.Latitude) * dx +
                       (point.Longitude - ligneDebut.Longitude) * dy) / (dx * dx + dy * dy);

            t = Math.Max(0, Math.Min(1, t));

            double projLat = ligneDebut.Latitude + t * dx;
            double projLng = ligneDebut.Longitude + t * dy;

            return CalculerDistanceHaversine(point.Latitude, point.Longitude, projLat, projLng);
        }

        /// <summary>Mapper un Trajet vers un résumé DTO</summary>
        private TrajetResumeDto MapToResume(Trajet trajet)
        {
            return new TrajetResumeDto
            {
                Id = trajet.Id,
                NomVehicule = trajet.NomVehicule,
                Immatriculation = trajet.Immatriculation,
                DateDebut = trajet.DateDebut,
                DateFin = trajet.DateFin,
                DureeFormatee = FormaterDuree(trajet.Duree),
                DistanceKm = trajet.DistanceKm,
                VitesseMoyenneKmh = trajet.VitesseMoyenneKmh,
                NombreArrets = trajet.NombreArrets,
                AdresseDepart = trajet.AdresseDepart,
                AdresseArrivee = trajet.AdresseArrivee,
                Statut = trajet.Statut,
                ConducteurNom = trajet.ConducteurNom
            };
        }

        // ==========================================================
        // DONNÉES SIMULÉES (Gatineau/Ottawa)
        // ==========================================================

        private static List<Trajet> GenererDonneesSimulees()
        {
            return new List<Trajet>
            {
                new Trajet
                {
                    Id = 1, VehiculeId = 1, NomVehicule = "Camion-01",
                    Immatriculation = "ABC 1234",
                    DateDebut = DateTime.UtcNow.AddDays(-1).AddHours(-8),
                    DateFin = DateTime.UtcNow.AddDays(-1).AddHours(-5.5),
                    DistanceKm = 45.3, VitesseMoyenneKmh = 38.2, VitesseMaxKmh = 72.0,
                    ConsommationLitres = 8.2, NombreArrets = 3,
                    TempsArretTotal = TimeSpan.FromMinutes(25),
                    AdresseDepart = "125 Rue Principale, Gatineau",
                    AdresseArrivee = "45 Boul. St-Joseph, Ottawa",
                    LatitudeDepart = 45.4765, LongitudeDepart = -75.7013,
                    LatitudeArrivee = 45.4215, LongitudeArrivee = -75.6972,
                    Statut = StatutTrajet.Termine,
                    ConducteurNom = "Jean Tremblay"
                },
                new Trajet
                {
                    Id = 2, VehiculeId = 1, NomVehicule = "Camion-01",
                    Immatriculation = "ABC 1234",
                    DateDebut = DateTime.UtcNow.AddDays(-2).AddHours(-10),
                    DateFin = DateTime.UtcNow.AddDays(-2).AddHours(-7),
                    DistanceKm = 62.8, VitesseMoyenneKmh = 42.5, VitesseMaxKmh = 85.0,
                    ConsommationLitres = 11.5, NombreArrets = 2,
                    TempsArretTotal = TimeSpan.FromMinutes(18),
                    AdresseDepart = "200 Boul. Maloney, Gatineau",
                    AdresseArrivee = "88 Rue Bank, Ottawa",
                    LatitudeDepart = 45.4832, LongitudeDepart = -75.6845,
                    LatitudeArrivee = 45.3876, LongitudeArrivee = -75.6912,
                    Statut = StatutTrajet.Termine,
                    ConducteurNom = "Jean Tremblay"
                },
                new Trajet
                {
                    Id = 3, VehiculeId = 2, NomVehicule = "Fourgon-05",
                    Immatriculation = "XYZ 5678",
                    DateDebut = DateTime.UtcNow.AddHours(-3),
                    DateFin = null,
                    DistanceKm = 18.5, VitesseMoyenneKmh = 32.0, VitesseMaxKmh = 55.0,
                    ConsommationLitres = 3.2, NombreArrets = 1,
                    TempsArretTotal = TimeSpan.FromMinutes(8),
                    AdresseDepart = "15 Rue Eddy, Gatineau",
                    AdresseArrivee = "",
                    LatitudeDepart = 45.4285, LongitudeDepart = -75.7145,
                    LatitudeArrivee = 0, LongitudeArrivee = 0,
                    Statut = StatutTrajet.EnCours,
                    ConducteurNom = "Marie Dubois"
                }
            };
        }
    }

    // ============================================================
    // CONTRÔLEUR API REST
    // -> deplace vers Controllers/TrajetsController.cs
    // ============================================================
}
