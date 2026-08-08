using GeoTrack.Api.Data;
using GeoTrack.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoTrack.Api.Services
{
    /// <summary>
    /// GEO-58 : implementation d'<see cref="INotificateurAlerteZone"/> qui, en
    /// plus de journaliser, consigne l'alerte dans la table centralisee Alertes.
    ///
    /// Remplace <see cref="NotificateurAlerteZoneJournal"/> dans Program.cs.
    /// Celui-ci reste disponible comme repli purement journalisant si l'on
    /// souhaite un jour desactiver la persistance sans toucher au code appelant.
    ///
    /// ORDRE DELIBERE : on journalise AVANT de persister. Si l'ecriture en base
    /// echoue, la trace de la sortie de zone existe quand meme dans les journaux.
    /// L'inverse ferait perdre les deux d'un coup.
    ///
    /// Scoped, car il depend du GeoTrackContext qui l'est aussi. C'est possible
    /// ici parce que le consommateur, PositionsGpsController, est lui-meme Scoped
    /// — contrairement a AlerteVitesseService qui, etant Singleton, ne peut pas
    /// dependre du contexte.
    /// </summary>
    public class NotificateurAlerteZonePersistant : INotificateurAlerteZone
    {
        private readonly GeoTrackContext _context;
        private readonly ILogger<NotificateurAlerteZonePersistant> _journal;

        public NotificateurAlerteZonePersistant(
            GeoTrackContext context,
            ILogger<NotificateurAlerteZonePersistant> journal)
        {
            _context = context;
            _journal = journal;
        }

        public async Task SignalerSortieDeZoneAsync(AlerteSortieZone alerte)
        {
            _journal.LogWarning(
                "Sortie de zone : vehicule {VehiculeId} a quitte la zone '{NomZone}' (#{ZoneId}). "
                + "Distance au centre {DistanceMetres:F0} m pour un rayon de {RayonMetres:F0} m. "
                + "Position {Latitude},{Longitude} a {Horodatage:o}.",
                alerte.VehiculeId, alerte.NomZone, alerte.ZoneId,
                alerte.DistanceMetres, alerte.RayonMetres,
                alerte.Latitude, alerte.Longitude, alerte.Horodatage);

            var entree = new Alerte
            {
                Date = alerte.Horodatage,
                VehiculeId = alerte.VehiculeId,
                TypeAlerte = TypeAlerte.SortieZone,
                // Une sortie de zone n'a pas de graduation dans le contrat GEO-9 :
                // tout franchissement compte pareil. On la classe donc en Alerte,
                // ni simple avertissement ni critique.
                Severite = SeveriteAlerte.Alerte,
                Details =
                    $"Sortie de la zone '{alerte.NomZone}' (#{alerte.ZoneId}) — "
                    + $"{alerte.DistanceMetres:F0} m du centre pour un rayon de "
                    + $"{alerte.RayonMetres:F0} m."
            };

            try
            {
                _context.Alertes.Add(entree);
                await _context.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                // Degradation gracieuse : perdre une ligne d'alerte est preferable
                // a faire echouer l'ingestion GPS, deja enregistree a ce stade.
                DetacherSansEchouer(entree);

                _journal.LogError(exception,
                    "Impossible de consigner l'alerte de sortie de zone pour le vehicule "
                    + "{VehiculeId}. L'evenement reste trace ci-dessus dans les journaux.",
                    alerte.VehiculeId);
            }
        }

        /// <summary>
        /// Retire l'entite du suivi du contexte, sans jamais lever.
        ///
        /// Detacher evite qu'un SaveChangesAsync ulterieur ne retente l'insertion
        /// echouee et propage la panne. Mais l'operation elle-meme peut echouer —
        /// typiquement si le contexte a ete libere — et une exception levee
        /// depuis un bloc catch reduirait a neant la degradation gracieuse qu'on
        /// cherche precisement a garantir ici.
        /// </summary>
        private void DetacherSansEchouer(Alerte entree)
        {
            try
            {
                _context.Entry(entree).State = EntityState.Detached;
            }
            catch (Exception exception)
            {
                _journal.LogDebug(exception,
                    "Le detachement de l'alerte non persistee a echoue ; sans consequence, "
                    + "le contexte est de toute facon inutilisable.");
            }
        }
    }
}
