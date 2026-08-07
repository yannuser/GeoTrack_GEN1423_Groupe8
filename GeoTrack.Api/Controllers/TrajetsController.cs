// ============================================================
// GEO-12 : Controleur API REST Historique Trajets
// Extrait de Services/GEO-12-HistoriqueTrajets-Service.cs
// (nettoyage structurel : les controleurs vivent sous Controllers/).
// Le service correspondant reste dans Services/.
// ============================================================

using System;
using System.Collections.Generic;
using GeoTrack.Api.Services.HistoriqueTrajets;
using Microsoft.AspNetCore.Mvc;

namespace GeoTrack.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrajetsController : ControllerBase
    {
        private readonly HistoriqueTrajetService _service = new();

        /// <summary>
        /// GET /api/trajets?vehiculeId=1&page=1&parPage=5
        /// Critère #1 : Lister l'historique avec date et durée
        /// </summary>
        [HttpGet]
        public ActionResult<ResultatPagine<TrajetResumeDto>> ListerTrajets(
            [FromQuery] RechercheTrajetRequest request)
        {
            if (request.VehiculeId <= 0)
                return BadRequest("VehiculeId est requis");

            var resultat = _service.ListerTrajets(request);
            return Ok(resultat);
        }

        /// <summary>
        /// GET /api/trajets/{id}/carte
        /// Critère #2 : Visualiser le trajet sur une carte
        /// </summary>
        [HttpGet("{id}/carte")]
        public ActionResult<TrajetDetailCarteDto> ObtenirTrajetCarte(int id)
        {
            var trajet = _service.ObtenirTrajetPourCarte(id);
            if (trajet == null)
                return NotFound($"Trajet {id} non trouvé");

            return Ok(trajet);
        }

        /// <summary>
        /// GET /api/trajets/statistiques?vehiculeId=1&debut=2026-07-01&fin=2026-08-01
        /// Statistiques d'utilisation sur période
        /// </summary>
        [HttpGet("statistiques")]
        public ActionResult<StatistiquesVehiculeDto> ObtenirStatistiques(
            [FromQuery] int vehiculeId,
            [FromQuery] DateTime debut,
            [FromQuery] DateTime fin)
        {
            if (vehiculeId <= 0)
                return BadRequest("VehiculeId est requis");

            if (debut >= fin)
                return BadRequest("La date de début doit être avant la date de fin");

            var stats = _service.ObtenirStatistiques(vehiculeId, debut, fin);
            return Ok(stats);
        }

        /// <summary>
        /// GET /api/trajets/export-csv?vehiculeId=1&debut=2026-07-01&fin=2026-08-01
        /// Export CSV des trajets
        /// </summary>
        [HttpGet("export-csv")]
        public ActionResult ExporterCsv(
            [FromQuery] int vehiculeId,
            [FromQuery] DateTime? debut,
            [FromQuery] DateTime? fin)
        {
            if (vehiculeId <= 0)
                return BadRequest("VehiculeId est requis");

            var csv = _service.ExporterCsv(vehiculeId, debut, fin);
            return File(System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv", $"trajets-vehicule-{vehiculeId}.csv");
        }

        /// <summary>
        /// POST /api/trajets/detecter
        /// Détecter automatiquement un trajet à partir de positions GPS brutes
        /// </summary>
        [HttpPost("detecter")]
        public ActionResult<Trajet> DetecterTrajet(
            [FromQuery] int vehiculeId,
            [FromBody] List<PointGps> positionsBrutes)
        {
            if (vehiculeId <= 0)
                return BadRequest("VehiculeId est requis");

            if (positionsBrutes == null || positionsBrutes.Count < 2)
                return BadRequest("Au moins 2 positions GPS sont requises");

            var trajet = _service.DetecterTrajet(positionsBrutes, vehiculeId);
            if (trajet == null)
                return BadRequest("Impossible de détecter un trajet (durée trop courte ou aucun mouvement)");

            return Ok(trajet);
        }
    }
}
