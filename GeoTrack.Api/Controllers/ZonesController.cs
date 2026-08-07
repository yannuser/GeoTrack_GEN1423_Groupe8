using GeoTrack.Api.Data;
using GeoTrack.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoTrack.Api.Controllers
{
    /// <summary>Corps attendu pour la creation d'une zone.</summary>
    public class CreerZoneRequest
    {
        public string? Nom { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RayonMetres { get; set; }
        public string? VehiculeId { get; set; }
        public TypeAlerteZone TypeAlerte { get; set; } = TypeAlerteZone.SortieZone;
    }

    /// <summary>
    /// GEO-9 : gestion des zones geographiques surveillees.
    ///
    /// [Authorize] au niveau du controleur, comme PositionsGpsController :
    /// definir ou supprimer une zone de surveillance est une operation
    /// d'administration, jamais anonyme.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/zones")]
    public class ZonesController : ControllerBase
    {
        private readonly GeoTrackContext _context;

        public ZonesController(GeoTrackContext context)
        {
            _context = context;
        }

        // POST api/zones
        [HttpPost]
        public async Task<IActionResult> Creer([FromBody] CreerZoneRequest? requete)
        {
            if (requete is null)
            {
                return BadRequest("Le corps de la requete est vide ou illisible.");
            }

            if (string.IsNullOrWhiteSpace(requete.Nom))
            {
                return BadRequest("Le champ Nom est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(requete.VehiculeId))
            {
                return BadRequest("Le champ VehiculeId est obligatoire.");
            }

            if (requete.Latitude < -90 || requete.Latitude > 90)
            {
                return BadRequest("Le champ Latitude doit etre compris entre -90 et 90.");
            }

            if (requete.Longitude < -180 || requete.Longitude > 180)
            {
                return BadRequest("Le champ Longitude doit etre compris entre -180 et 180.");
            }

            if (requete.RayonMetres <= 0)
            {
                return BadRequest("Le champ RayonMetres doit etre strictement positif.");
            }

            var zone = new ZoneGeographique
            {
                Nom = requete.Nom.Trim(),
                Latitude = requete.Latitude,
                Longitude = requete.Longitude,
                RayonMetres = requete.RayonMetres,
                VehiculeId = requete.VehiculeId.Trim(),
                TypeAlerte = requete.TypeAlerte,
                DateCreation = DateTime.UtcNow
            };

            _context.ZonesGeographiques.Add(zone);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Obtenir), new { id = zone.Id }, zone);
        }

        // GET api/zones
        // GET api/zones?vehiculeId=VEH-001
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ZoneGeographique>>> Lister(
            [FromQuery] string? vehiculeId = null)
        {
            var requete = _context.ZonesGeographiques.AsQueryable();

            if (!string.IsNullOrWhiteSpace(vehiculeId))
            {
                var recherche = vehiculeId.Trim();
                requete = requete.Where(z => z.VehiculeId == recherche);
            }

            var zones = await requete
                .OrderByDescending(z => z.DateCreation)
                .ToListAsync();

            return Ok(zones);
        }

        // GET api/zones/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ZoneGeographique>> Obtenir(int id)
        {
            var zone = await _context.ZonesGeographiques.FirstOrDefaultAsync(z => z.Id == id);

            if (zone is null)
            {
                return NotFound($"Zone #{id} introuvable.");
            }

            return Ok(zone);
        }

        // DELETE api/zones/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Supprimer(int id)
        {
            var zone = await _context.ZonesGeographiques.FirstOrDefaultAsync(z => z.Id == id);

            if (zone is null)
            {
                return NotFound($"Zone #{id} introuvable.");
            }

            _context.ZonesGeographiques.Remove(zone);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
