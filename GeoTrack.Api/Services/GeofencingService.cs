using GeoTrack.Api.Models;

namespace GeoTrack.Api.Services
{
    /// <summary>Position d'un vehicule vis-a-vis d'une zone a un instant donne.</summary>
    public enum EtatZone
    {
        /// <summary>Aucune position anterieure connue : l'etat n'est pas determinable.</summary>
        Inconnu = 0,
        Interieur = 1,
        Exterieur = 2
    }

    /// <summary>
    /// Resultat de l'evaluation d'UNE zone pour une position donnee.
    /// </summary>
    public class EvaluationZone
    {
        public required ZoneGeographique Zone { get; init; }

        /// <summary>Distance entre la position evaluee et le centre de la zone, en metres.</summary>
        public double DistanceMetres { get; init; }

        public EtatZone EtatPrecedent { get; init; }

        public EtatZone EtatActuel { get; init; }

        /// <summary>
        /// Vrai uniquement sur la TRANSITION interieur -> exterieur.
        /// </summary>
        public bool SortieDetectee { get; init; }
    }

    /// <summary>
    /// Alerte emise lorsqu'un vehicule quitte une zone surveillee.
    /// </summary>
    public class AlerteSortieZone
    {
        public required string VehiculeId { get; init; }
        public int ZoneId { get; init; }
        public required string NomZone { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double DistanceMetres { get; init; }
        public double RayonMetres { get; init; }
        public DateTime Horodatage { get; init; }
    }

    /// <summary>
    /// GEO-9 : point de branchement pour la suite donnee a une sortie de zone.
    ///
    /// L'implementation actuelle se contente de journaliser (voir
    /// <see cref="NotificateurAlerteZoneJournal"/>). Le chantier GEO-51/GEO-58
    /// (stockage centralise des alertes) n'aura qu'a fournir une autre
    /// implementation et changer la ligne d'enregistrement dans Program.cs :
    /// aucun appelant n'a besoin d'etre modifie.
    /// </summary>
    public interface INotificateurAlerteZone
    {
        Task SignalerSortieDeZoneAsync(AlerteSortieZone alerte);
    }

    /// <summary>
    /// Implementation par defaut : journalisation via ILogger.
    /// Provisoire, en attendant la persistance reelle des alertes.
    /// </summary>
    public class NotificateurAlerteZoneJournal : INotificateurAlerteZone
    {
        private readonly ILogger<NotificateurAlerteZoneJournal> _journal;

        public NotificateurAlerteZoneJournal(ILogger<NotificateurAlerteZoneJournal> journal)
        {
            _journal = journal;
        }

        public Task SignalerSortieDeZoneAsync(AlerteSortieZone alerte)
        {
            _journal.LogWarning(
                "Sortie de zone : vehicule {VehiculeId} a quitte la zone '{NomZone}' (#{ZoneId}). "
                + "Distance au centre {DistanceMetres:F0} m pour un rayon de {RayonMetres:F0} m. "
                + "Position {Latitude},{Longitude} a {Horodatage:o}.",
                alerte.VehiculeId, alerte.NomZone, alerte.ZoneId,
                alerte.DistanceMetres, alerte.RayonMetres,
                alerte.Latitude, alerte.Longitude, alerte.Horodatage);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// GEO-9 : detection d'entree/sortie de zone geographique.
    ///
    /// Ce service est volontairement PUR : aucune dependance, aucun etat
    /// interne. L'etat precedent lui est fourni par l'appelant sous la forme de
    /// la position anterieure du vehicule. Trois consequences :
    ///  - il est testable sans base ni conteneur d'injection ;
    ///  - il ne « perd » pas la memoire au redemarrage de l'API, contrairement
    ///    a un cache en memoire ;
    ///  - plusieurs instances de l'API donneraient le meme verdict.
    /// </summary>
    public class GeofencingService
    {
        /// <summary>Rayon moyen de la Terre, en metres (formule de Haversine).</summary>
        private const double RayonTerreMetres = 6_371_000.0;

        /// <summary>
        /// Evalue chaque zone pour la position courante et signale les sorties.
        ///
        /// Une sortie n'est retenue que sur la TRANSITION interieur -> exterieur.
        /// Un vehicule deja dehors au tick precedent ne redeclenche donc rien,
        /// ce qui evite une alerte a chaque position recue.
        /// </summary>
        /// <param name="zones">Zones surveillant le vehicule concerne.</param>
        /// <param name="positionActuelle">Position qui vient d'etre recue.</param>
        /// <param name="positionPrecedente">
        /// Derniere position connue AVANT celle-ci, ou null s'il n'y en a pas.
        /// Dans ce cas l'etat precedent vaut <see cref="EtatZone.Inconnu"/> et
        /// aucune sortie n'est signalee : on ne peut pas affirmer que le
        /// vehicule vient de sortir si on ignore ou il etait.
        /// </param>
        public IReadOnlyList<EvaluationZone> Evaluer(
            IEnumerable<ZoneGeographique> zones,
            PositionGps positionActuelle,
            PositionGps? positionPrecedente)
        {
            ArgumentNullException.ThrowIfNull(zones);
            ArgumentNullException.ThrowIfNull(positionActuelle);

            var evaluations = new List<EvaluationZone>();

            foreach (var zone in zones)
            {
                var distance = CalculerDistanceMetres(
                    positionActuelle.Latitude, positionActuelle.Longitude,
                    zone.Latitude, zone.Longitude);

                var etatActuel = distance <= zone.RayonMetres
                    ? EtatZone.Interieur
                    : EtatZone.Exterieur;

                var etatPrecedent = positionPrecedente is null
                    ? EtatZone.Inconnu
                    : DeterminerEtat(zone, positionPrecedente.Latitude, positionPrecedente.Longitude);

                evaluations.Add(new EvaluationZone
                {
                    Zone = zone,
                    DistanceMetres = distance,
                    EtatPrecedent = etatPrecedent,
                    EtatActuel = etatActuel,
                    SortieDetectee = etatPrecedent == EtatZone.Interieur
                                     && etatActuel == EtatZone.Exterieur
                });
            }

            return evaluations;
        }

        /// <summary>
        /// Etat instantane d'un point vis-a-vis d'une zone.
        /// Le point situe exactement sur le cercle est considere DANS la zone.
        /// </summary>
        public EtatZone DeterminerEtat(ZoneGeographique zone, double latitude, double longitude)
        {
            ArgumentNullException.ThrowIfNull(zone);

            var distance = CalculerDistanceMetres(latitude, longitude, zone.Latitude, zone.Longitude);

            return distance <= zone.RayonMetres ? EtatZone.Interieur : EtatZone.Exterieur;
        }

        /// <summary>
        /// Distance orthodromique entre deux points GPS, en metres (Haversine).
        /// </summary>
        public double CalculerDistanceMetres(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = EnRadians(lat2 - lat1);
            var dLon = EnRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(EnRadians(lat1)) * Math.Cos(EnRadians(lat2))
                    * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return RayonTerreMetres * c;
        }

        private static double EnRadians(double degres) => degres * Math.PI / 180.0;
    }
}
