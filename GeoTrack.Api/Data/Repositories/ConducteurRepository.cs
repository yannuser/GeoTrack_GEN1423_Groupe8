using GeoTrack.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GeoTrack.Api.Data.Repositories
{
    /// <summary>
    /// GEO-15 : implementation Entity Framework Core d'<see cref="IConducteurRepository"/>.
    ///
    /// Le contrat ne definit nulle part ce qu'est un conducteur « disponible ».
    /// Critere retenu : un conducteur est disponible tant qu'aucun vehicule ne le
    /// reference via <c>Vehicule.ConducteurId</c>. Il est donc derive des donnees
    /// existantes, sans drapeau a maintenir.
    /// </summary>
    public class ConducteurRepository : IConducteurRepository
    {
        private readonly GeoTrackContext _context;

        public ConducteurRepository(GeoTrackContext context)
        {
            _context = context;
        }

        public async Task<List<(int Id, string Nom)>> GetDisponiblesAsync()
        {
            var idsAffectes = _context.Vehicules
                .Where(v => v.ConducteurId != null)
                .Select(v => v.ConducteurId!.Value);

            // La projection vers un ValueTuple n'est pas traduisible en SQL :
            // on materialise d'abord, on projette ensuite en memoire.
            var disponibles = await _context.Conducteurs
                .Where(c => !idsAffectes.Contains(c.Id))
                .OrderBy(c => c.Nom)
                .ToListAsync();

            return disponibles
                .Select(c => (c.Id, c.Nom))
                .ToList();
        }
    }
}
