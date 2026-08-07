// ============================================================================
// GEO-16 : Controleur API Health & Resilience
// Extrait de Services/GEO-16-Resilience-Service.cs
// (nettoyage structurel : les controleurs vivent sous Controllers/).
// Le service correspondant reste dans Services/.
//
// NOTE : les attributs [ApiController]/[Route] sont volontairement laisses en
// commentaire, tels qu'ecrits a l'origine. Cette classe n'est donc pas encore
// exposee comme endpoint HTTP. Aucun changement de logique n'a ete fait ici.
// ============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using GeoTrack.Api.Services.Resilience;

namespace GeoTrack.Api.Controllers
{
    // [ApiController]
    // [Route("api/health")]
    public class HealthController
    {
        private readonly ResilienceService _resilience;

        public HealthController(ResilienceService resilience)
        {
            _resilience = resilience;
        }

        // GET /api/health
        // Critère #1 : vérifier qu'une panne partielle n'interrompt pas tout
        public async Task<RapportSanteGlobal> ObtenirSanteGlobale()
        {
            return await _resilience.ObtenirRapportSante();
        }

        // GET /api/health/composants
        public List<EtatCircuit> ObtenirCircuitBreakers()
        {
            return _resilience.ObtenirEtatsCircuits();
        }

        // GET /api/health/metriques
        public List<MetriquesResilience> ObtenirMetriques()
        {
            return _resilience.ObtenirMetriques();
        }

        // GET /api/health/failover
        // Critère #2 : mécanismes de secours documentés
        public List<ConfigFailover> ObtenirFailovers()
        {
            return _resilience.ObtenirConfigsFailover();
        }

        // GET /api/health/evenements?limite=50
        public List<EvenementResilience> ObtenirEvenements(int limite = 50)
        {
            return _resilience.ObtenirEvenements(limite);
        }

        // POST /api/health/reset/{composant}
        public void ResetCircuit(ComposantSysteme composant)
        {
            _resilience.ResetCircuit(composant);
        }
    }
}
