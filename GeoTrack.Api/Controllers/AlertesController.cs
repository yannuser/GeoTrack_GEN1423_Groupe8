using GeoTrack.Api.Data;
using GeoTrack.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoTrack.Api.Controllers
{
    /// <summary>
    /// GEO-58 : consultation de l'historique centralise des alertes.
    ///
    /// [Authorize] au niveau du controleur, comme PositionsGpsController et
    /// ZonesController : l'historique des alertes revele les deplacements de la
    /// flotte et n'a rien a faire en acces anonyme.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/alertes")]
    public class AlertesController : ControllerBase
    {
        private readonly GeoTrackContext _context;

        public AlertesController(GeoTrackContext context)
        {
            _context = context;
        }

        // GET api/alertes
        // GET api/alertes?vehiculeId=VEH-001
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Alerte>>> Lister(
            [FromQuery] string? vehiculeId = null)
        {
            var requete = _context.Alertes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(vehiculeId))
            {
                var recherche = vehiculeId.Trim();
                requete = requete.Where(a => a.VehiculeId == recherche);
            }

            var alertes = await requete
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            return Ok(alertes);
        }
    }
}
