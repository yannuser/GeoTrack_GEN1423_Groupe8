// ============================================================================
// GEO-16 : Controleur API Health & Resilience
// Service correspondant : Services/GEO-16-Resilience-Service.cs
//
// Les attributs [ApiController]/[Route] sont desormais actifs : le controleur
// est expose sous /api/health. Les attributs de verbe ([HttpGet]/[HttpPost])
// ont ete ajoutes sur chaque action, en reprenant a l'identique les routes
// deja documentees en commentaire par l'auteur d'origine. Sans eux, les six
// actions repondaient toutes a "api/health" et ASP.NET Core levait une
// AmbiguousMatchException : decommenter les deux attributs ne suffisait pas.
//
// Volontairement accessible sans authentification : un endpoint de sante doit
// rester interrogeable par une sonde externe (supervision, load balancer).
// ============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using GeoTrack.Api.Services.Resilience;
using Microsoft.AspNetCore.Mvc;

namespace GeoTrack.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly ResilienceService _resilience;

        public HealthController(ResilienceService resilience)
        {
            _resilience = resilience;
        }

        // GET /api/health
        // Critère #1 : vérifier qu'une panne partielle n'interrompt pas tout
        [HttpGet]
        public async Task<RapportSanteGlobal> ObtenirSanteGlobale()
        {
            return await _resilience.ObtenirRapportSante();
        }

        // GET /api/health/composants
        [HttpGet("composants")]
        public List<EtatCircuit> ObtenirCircuitBreakers()
        {
            return _resilience.ObtenirEtatsCircuits();
        }

        // GET /api/health/metriques
        [HttpGet("metriques")]
        public List<MetriquesResilience> ObtenirMetriques()
        {
            return _resilience.ObtenirMetriques();
        }

        // GET /api/health/failover
        // Critère #2 : mécanismes de secours documentés
        [HttpGet("failover")]
        public List<ConfigFailover> ObtenirFailovers()
        {
            return _resilience.ObtenirConfigsFailover();
        }

        // GET /api/health/evenements?limite=50
        [HttpGet("evenements")]
        public List<EvenementResilience> ObtenirEvenements([FromQuery] int limite = 50)
        {
            return _resilience.ObtenirEvenements(limite);
        }

        // POST /api/health/reset/{composant}
        [HttpPost("reset/{composant}")]
        public void ResetCircuit(ComposantSysteme composant)
        {
            _resilience.ResetCircuit(composant);
        }
    }
}
