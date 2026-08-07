using System;

namespace GeoTrack.Api.Models
{
    /// <summary>
    /// GEO-9 : zone geographique circulaire surveillee pour un vehicule.
    ///
    /// La zone est definie par un centre (Latitude/Longitude) et un rayon en
    /// metres. Le choix du cercle plutot que du polygone est deliberé : il
    /// couvre le critere d'acceptation (detection de sortie de zone) avec un
    /// seul calcul de distance, sans dependance geospatiale supplementaire.
    ///
    /// VehiculeId est une chaine, pour s'aligner sur PositionGps.VehiculeId qui
    /// identifie deja les vehicules ainsi dans le flux GPS (GEO-8).
    /// </summary>
    public class ZoneGeographique
    {
        public int Id { get; set; }

        public string Nom { get; set; } = string.Empty;

        /// <summary>Latitude du centre de la zone, en degres decimaux.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude du centre de la zone, en degres decimaux.</summary>
        public double Longitude { get; set; }

        /// <summary>Rayon de la zone, en metres. Doit etre strictement positif.</summary>
        public double RayonMetres { get; set; }

        /// <summary>
        /// Vehicule surveille par cette zone. Correspond a
        /// <see cref="PositionGps.VehiculeId"/>.
        /// </summary>
        public string VehiculeId { get; set; } = string.Empty;

        public TypeAlerteZone TypeAlerte { get; set; } = TypeAlerteZone.SortieZone;

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    }
}
