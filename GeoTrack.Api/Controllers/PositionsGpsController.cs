using GeoTrack.Api.Data;
using GeoTrack.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoTrack.Api.Controllers
{
    [ApiController]
    [Route("api/positionsgps")]
    public class PositionsGpsController : ControllerBase
    {
        private readonly GeoTrackContext _context;

        public PositionsGpsController(GeoTrackContext context)
        {
            _context = context;
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

            _context.PositionsGps.Add(position);
            await _context.SaveChangesAsync();

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
    }
}
