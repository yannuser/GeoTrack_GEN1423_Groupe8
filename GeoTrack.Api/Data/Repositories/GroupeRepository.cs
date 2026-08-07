using GeoTrack.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GeoTrack.Api.Data.Repositories
{
    /// <summary>
    /// GEO-15 : implementation Entity Framework Core d'<see cref="IGroupeRepository"/>.
    ///
    /// Aucune table « Groupes » n'est creee : le contrat de GEO-15 traite le
    /// groupe comme du texte libre (<c>Vehicule.GroupeDivision</c> est un string
    /// sans cle etrangere, et l'interface renvoie une simple List&lt;string&gt;).
    /// Une table dediee n'apporterait aucune integrite referentielle et devrait
    /// etre amorcee a la main. La liste est donc derivee des vehicules existants,
    /// ce qui la garde toujours synchronisee avec la realite.
    /// </summary>
    public class GroupeRepository : IGroupeRepository
    {
        private readonly GeoTrackContext _context;

        public GroupeRepository(GeoTrackContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetAllAsync()
            => await _context.Vehicules
                .Where(v => v.GroupeDivision != null && v.GroupeDivision != "")
                .Select(v => v.GroupeDivision!)
                .Distinct()
                .OrderBy(g => g)
                .ToListAsync();
    }
}
