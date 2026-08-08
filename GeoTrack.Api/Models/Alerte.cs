using System;
using GeoTrack.Api.Services;

namespace GeoTrack.Api.Models
{
    /// <summary>
    /// GEO-58 : alerte consignee, toutes sources confondues.
    ///
    /// Deux producteurs alimentent cette table : le depassement de vitesse
    /// (GEO-51, via AlerteVitesseService) et la sortie de zone (GEO-9, via
    /// INotificateurAlerteZone). Les regrouper ici donne au tableau de bord un
    /// seul endroit a interroger, et a l'historique un tri chronologique unique.
    ///
    /// NOTE : <see cref="Severite"/> reutilise volontairement l'enum
    /// <see cref="SeveriteAlerte"/> definie dans GeoTrack.Api.Services (GEO-51)
    /// plutot que d'en dupliquer une dans Models. Cela cree une dependance de
    /// Models vers Services, inhabituelle mais preferable a deux enums
    /// concurrentes qui divergeraient — le depot a deja paye ce prix ailleurs.
    /// </summary>
    public class Alerte
    {
        public int Id { get; set; }

        /// <summary>
        /// Date de l'evenement ayant provoque l'alerte, et non date d'insertion :
        /// c'est l'horodatage de la position GPS qui fait foi, pour rester
        /// coherent avec le reste de la chaine de traitement.
        /// </summary>
        public DateTime Date { get; set; }

        public string VehiculeId { get; set; } = string.Empty;

        public TypeAlerte TypeAlerte { get; set; }

        public SeveriteAlerte Severite { get; set; }

        /// <summary>Description lisible : vitesse relevee, zone quittee, etc.</summary>
        public string Details { get; set; } = string.Empty;
    }
}
