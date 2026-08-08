using GeoTrack.Api.Data;
using GeoTrack.Api.Models;
using GeoTrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoTrack.Api.Controllers
{
    // GEO-18 : les positions GPS ne sont plus accessibles anonymement.
    // [Authorize] au niveau du controleur couvre GET et POST : toute requete
    // sans jeton JWT valide recoit un 401 avant d'atteindre les actions.
    [ApiController]
    [Authorize]
    [Route("api/positionsgps")]
    public class PositionsGpsController : ControllerBase
    {
        private readonly GeoTrackContext _context;
        private readonly GeofencingService _geofencing;
        private readonly INotificateurAlerteZone _notificateur;
        private readonly AlerteVitesseService _alerteVitesse;
        private readonly ILogger<PositionsGpsController> _journal;

        public PositionsGpsController(
            GeoTrackContext context,
            GeofencingService geofencing,
            INotificateurAlerteZone notificateur,
            AlerteVitesseService alerteVitesse,
            ILogger<PositionsGpsController> journal)
        {
            _context = context;
            _geofencing = geofencing;
            _notificateur = notificateur;
            _alerteVitesse = alerteVitesse;
            _journal = journal;
        }

        // POST api/positionsgps
        [HttpPost]
        public async Task<IActionResult> Recevoir([FromBody] PositionGps position)
        {
            if (position is null)
            {
                return BadRequest("Le corps de la requete est vide ou illisible.");
            }

            if (string.IsNullOrWhiteSpace(position.VehiculeId))
            {
                return BadRequest("Le champ VehiculeId est obligatoire.");
            }

            if (position.Latitude < -90 || position.Latitude > 90)
            {
                return BadRequest("Le champ Latitude doit etre compris entre -90 et 90.");
            }

            if (position.Longitude < -180 || position.Longitude > 180)
            {
                return BadRequest("Le champ Longitude doit etre compris entre -180 et 180.");
            }

            if (position.Horodatage == default)
            {
                return BadRequest("Le champ Horodatage est obligatoire.");
            }

            // GEO-9 : la position anterieure doit etre lue AVANT d'enregistrer la
            // nouvelle, sans quoi celle qui vient d'arriver serait prise pour son
            // propre antecedent et aucune transition ne serait jamais detectee.
            var positionPrecedente = await _context.PositionsGps
                .Where(p => p.VehiculeId == position.VehiculeId)
                .OrderByDescending(p => p.Horodatage)
                .FirstOrDefaultAsync();

            _context.PositionsGps.Add(position);
            await _context.SaveChangesAsync();

            await VerifierSortiesDeZoneAsync(position, positionPrecedente);
            await VerifierDepassementVitesseAsync(position);

            return Ok(position);
        }

        // GET api/positionsgps
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PositionGps>>> Dernieres()
        {
            var positions = await _context.PositionsGps
                .OrderByDescending(p => p.Horodatage)
                .Take(50)
                .ToListAsync();

            return Ok(positions);
        }

        /// <summary>
        /// GEO-9 : confronte la nouvelle position aux zones du vehicule et
        /// signale chaque sortie detectee.
        ///
        /// Le geofencing ne doit jamais faire echouer l'ingestion GPS : la
        /// position est deja enregistree quand cette methode s'execute, et une
        /// defaillance de la notification est journalisee sans remonter en erreur
        /// HTTP. Perdre une alerte est preferable a perdre une position.
        /// </summary>
        private async Task VerifierSortiesDeZoneAsync(
            PositionGps position, PositionGps? positionPrecedente)
        {
            try
            {
                var zones = await _context.ZonesGeographiques
                    .Where(z => z.VehiculeId == position.VehiculeId)
                    .ToListAsync();

                if (zones.Count == 0)
                {
                    return;
                }

                var evaluations = _geofencing.Evaluer(zones, position, positionPrecedente);

                foreach (var evaluation in evaluations.Where(e => e.SortieDetectee))
                {
                    await _notificateur.SignalerSortieDeZoneAsync(new AlerteSortieZone
                    {
                        VehiculeId = position.VehiculeId,
                        ZoneId = evaluation.Zone.Id,
                        NomZone = evaluation.Zone.Nom,
                        Latitude = position.Latitude,
                        Longitude = position.Longitude,
                        DistanceMetres = evaluation.DistanceMetres,
                        RayonMetres = evaluation.Zone.RayonMetres,
                        Horodatage = position.Horodatage
                    });
                }
            }
            catch (Exception exception)
            {
                // La position est deja enregistree a ce stade. On journalise et
                // on rend la main : un geofencing en panne ne doit pas se
                // traduire par un 500 cote emetteur GPS, qui reessaierait et
                // dupliquerait la position. Couvre notamment le cas ou la
                // migration GEO9_ZonesGeographiques n'a pas encore ete appliquee.
                _journal.LogError(exception,
                    "Geofencing indisponible pour le vehicule {VehiculeId}. "
                    + "La position a bien ete enregistree, la verification de zone est ignoree.",
                    position.VehiculeId);
            }
        }

        /// <summary>
        /// GEO-58 : soumet la vitesse de la position a AlerteVitesseService
        /// (GEO-51) et consigne l'alerte en base si elle est effectivement
        /// declenchee.
        ///
        /// La condition de persistance est <c>AlerteEnvoyee</c>, et non l'etat
        /// Declenchee/Escaladee seul : un vehicule peut rester en etat Declenchee
        /// pendant plusieurs mesures alors que l'anti-spam de GEO-51 bloque les
        /// alertes suivantes. Se fier a l'etat inonderait la table d'un doublon
        /// par position recue, ce qui reviendrait a contourner l'anti-spam. Quand
        /// AlerteEnvoyee vaut vrai, l'etat est de toute facon Declenchee ou
        /// Escaladee.
        ///
        /// Meme degradation gracieuse que le geofencing : la position est deja
        /// enregistree, une panne ici ne doit pas produire de 500.
        /// </summary>
        private async Task VerifierDepassementVitesseAsync(PositionGps position)
        {
            Alerte? entree = null;

            try
            {
                var resultat = await _alerteVitesse.EvaluerVitesse(new DonneeVitesse
                {
                    AppareilId = position.VehiculeId,
                    Vitesse = position.Vitesse,
                    Latitude = position.Latitude,
                    Longitude = position.Longitude,
                    Horodatage = position.Horodatage
                });

                if (!resultat.AlerteEnvoyee)
                {
                    return;
                }

                entree = new Alerte
                {
                    Date = position.Horodatage,
                    VehiculeId = position.VehiculeId,
                    TypeAlerte = TypeAlerte.VitesseExcessive,
                    Severite = resultat.Severite,
                    Details =
                        $"Vitesse relevee {resultat.VitesseMesuree:F1} km/h pour un seuil de "
                        + $"{resultat.SeuilDepasse:F1} km/h (etat {resultat.Etat}). {resultat.Raison}"
                };

                _context.Alertes.Add(entree);
                await _context.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                // Detacher l'entite si elle a ete ajoutee : sans cela elle
                // resterait suivie par le contexte et un SaveChangesAsync
                // ulterieur la retenterait, propageant l'echec. Le detachement
                // est lui-meme protege : lever depuis un bloc catch annulerait
                // la degradation gracieuse et rendrait un 500 a l'emetteur GPS.
                if (entree is not null)
                {
                    try
                    {
                        _context.Entry(entree).State = EntityState.Detached;
                    }
                    catch (Exception echecDetachement)
                    {
                        _journal.LogDebug(echecDetachement,
                            "Detachement de l'alerte de vitesse non persistee impossible.");
                    }
                }

                _journal.LogError(exception,
                    "Surveillance de vitesse indisponible pour le vehicule {VehiculeId}. "
                    + "La position a bien ete enregistree, l'evaluation est ignoree.",
                    position.VehiculeId);
            }
        }
    }
}
