// ============================================================
// GEO-15 : Controleur API REST Vehicules
// Extrait de Services/GEO-15-VehiculeCrud-Service.cs
// (nettoyage structurel : les controleurs vivent sous Controllers/).
// Le service correspondant reste dans Services/.
// ============================================================

using System.Linq;
using System.Threading.Tasks;
using GeoTrack.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GeoTrack.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehiculesController : ControllerBase
    {
        private readonly VehiculeService _service;

        public VehiculesController(VehiculeService service)
        {
            _service = service;
        }

        /// <summary>
        /// POST /api/vehicules — Ajouter un nouveau véhicule
        /// Critère d'acceptation #1 : formulaire d'ajout disponible
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Creer([FromBody] CreerVehiculeRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.Identity?.Name ?? "admin";
            var resultat = await _service.CreerVehiculeAsync(request, userId);

            if (!resultat.Succes)
                return Conflict(new { resultat.Message, resultat.Erreurs });

            return CreatedAtAction(nameof(GetById),
                new { id = resultat.Donnees.Id }, resultat);
        }

        /// <summary>
        /// POST /api/vehicules/position-gps — Recevoir position GPS
        /// Critère d'acceptation #2 : apparition carte à 1ère position
        /// </summary>
        [HttpPost("position-gps")]
        public async Task<IActionResult> RecevoirPositionGps([FromBody] PositionGpsEvent positionEvent)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var resultat = await _service.TraiterPositionGpsAsync(positionEvent);

            if (!resultat.Succes)
                return NotFound(new { resultat.Message });

            return Ok(resultat);
        }

        /// <summary>
        /// GET /api/vehicules/{id} — Obtenir un véhicule par ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var resultat = await _service.ListerVehiculesAsync();
            var vehicule = resultat.Donnees?.FirstOrDefault(v => v.Id == id);

            if (vehicule == null)
                return NotFound(new { Message = $"Véhicule #{id} introuvable." });

            return Ok(vehicule);
        }

        /// <summary>
        /// GET /api/vehicules — Lister tous les véhicules (filtre optionnel)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Lister([FromQuery] StatutVehicule? statut = null)
        {
            var resultat = await _service.ListerVehiculesAsync(statut);
            return Ok(resultat);
        }

        /// <summary>
        /// PUT /api/vehicules/{id} — Modifier un véhicule
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Modifier(int id, [FromBody] ModifierVehiculeRequest request)
        {
            var resultat = await _service.ModifierVehiculeAsync(id, request);

            if (!resultat.Succes)
                return resultat.Message.Contains("introuvable")
                    ? NotFound(new { resultat.Message })
                    : Conflict(new { resultat.Message });

            return Ok(resultat);
        }

        /// <summary>
        /// DELETE /api/vehicules/{id} — Supprimer un véhicule
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Supprimer(int id)
        {
            var resultat = await _service.SupprimerVehiculeAsync(id);

            if (!resultat.Succes)
                return resultat.Message.Contains("introuvable")
                    ? NotFound(new { resultat.Message })
                    : Conflict(new { resultat.Message });

            return Ok(resultat);
        }

        /// <summary>
        /// GET /api/vehicules/verifier-immatriculation?value=ABC123
        /// Validation unicité en temps réel
        /// </summary>
        [HttpGet("verifier-immatriculation")]
        public async Task<IActionResult> VerifierImmatriculation(
            [FromQuery] string value, [FromQuery] int? excludeId = null)
        {
            var disponible = await _service.VerifierUniciteImmatriculationAsync(value, excludeId);
            return Ok(new { disponible });
        }

        /// <summary>
        /// GET /api/vehicules/verifier-tracker?value=TRK-001
        /// </summary>
        [HttpGet("verifier-tracker")]
        public async Task<IActionResult> VerifierTracker(
            [FromQuery] string value, [FromQuery] int? excludeId = null)
        {
            var disponible = await _service.VerifierUniciteTrackerAsync(value, excludeId);
            return Ok(new { disponible });
        }

        /// <summary>
        /// GET /api/vehicules/conducteurs — Liste conducteurs disponibles
        /// </summary>
        [HttpGet("conducteurs")]
        public async Task<IActionResult> GetConducteurs()
        {
            var liste = await _service.GetConducteursDisponiblesAsync();
            return Ok(liste.Select(c => new { c.Id, c.Nom }));
        }

        /// <summary>
        /// GET /api/vehicules/groupes — Liste groupes/divisions
        /// </summary>
        [HttpGet("groupes")]
        public async Task<IActionResult> GetGroupes()
        {
            var liste = await _service.GetGroupesAsync();
            return Ok(liste);
        }
    }
}
