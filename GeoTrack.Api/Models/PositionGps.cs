using System;

namespace GeoTrack.Api.Models
{
    public class PositionGps
    {
        public int Id { get; set; }
        public string VehiculeId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Vitesse { get; set; }
        public int Direction { get; set; }
        public DateTime Horodatage { get; set; }
        public string EtatVehicule { get; set; } = string.Empty;
        public double? NiveauCarburant { get; set; }
        public string? Erreur { get; set; }
    }
}