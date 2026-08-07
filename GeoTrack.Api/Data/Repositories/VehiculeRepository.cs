using GeoTrack.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GeoTrack.Api.Data.Repositories
{
    /// <summary>
    /// GEO-15 : implementation Entity Framework Core d'<see cref="IVehiculeRepository"/>.
    ///
    /// NOTE sur les retours null : le contrat declare <c>Task&lt;Vehicule&gt;</c>
    /// (non-nullable) alors que VehiculeService teste explicitement <c>== null</c>
    /// sur chaque resultat. L'operateur <c>!</c> sert donc a respecter la
    /// signature de Sory sans modifier son interface : la valeur peut bel et bien
    /// etre null, et le service compte dessus.
    /// </summary>
    public class VehiculeRepository : IVehiculeRepository
    {
        private readonly GeoTrackContext _context;

        public VehiculeRepository(GeoTrackContext context)
        {
            _context = context;
        }

        // ----------------------------------------------------------------
        // LECTURES UNITAIRES
        // ----------------------------------------------------------------

        public async Task<Vehicule> GetByIdAsync(int id)
            => (await _context.Vehicules.FirstOrDefaultAsync(v => v.Id == id))!;

        public async Task<Vehicule> GetByImmatriculationAsync(string immatriculation)
            => (await _context.Vehicules
                .FirstOrDefaultAsync(v => v.Immatriculation == immatriculation))!;

        public async Task<Vehicule> GetByTrackerGpsIdAsync(string trackerGpsId)
            => (await _context.Vehicules
                .FirstOrDefaultAsync(v => v.TrackerGpsId == trackerGpsId))!;

        public async Task<Vehicule> GetByVINAsync(string vin)
            => (await _context.Vehicules.FirstOrDefaultAsync(v => v.VIN == vin))!;

        // ----------------------------------------------------------------
        // LISTES
        // ----------------------------------------------------------------

        public async Task<List<Vehicule>> GetAllAsync()
            => await _context.Vehicules
                .OrderBy(v => v.Immatriculation)
                .ToListAsync();

        public async Task<List<Vehicule>> GetByStatutAsync(StatutVehicule statut)
            => await _context.Vehicules
                .Where(v => v.Statut == statut)
                .OrderBy(v => v.Immatriculation)
                .ToListAsync();

        // ----------------------------------------------------------------
        // ECRITURES
        // ----------------------------------------------------------------

        public async Task<Vehicule> CreateAsync(Vehicule vehicule)
        {
            _context.Vehicules.Add(vehicule);
            await _context.SaveChangesAsync();
            return vehicule;
        }

        public async Task<Vehicule> UpdateAsync(Vehicule vehicule)
        {
            // Le service modifie une instance deja suivie par le contexte
            // (obtenue via GetByIdAsync / GetByTrackerGpsIdAsync). Update() est
            // neanmoins pose explicitement pour couvrir le cas d'une entite
            // detachee, sans effet de bord si elle est deja suivie.
            _context.Vehicules.Update(vehicule);
            await _context.SaveChangesAsync();
            return vehicule;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var vehicule = await _context.Vehicules.FirstOrDefaultAsync(v => v.Id == id);
            if (vehicule == null)
                return false;

            _context.Vehicules.Remove(vehicule);
            await _context.SaveChangesAsync();
            return true;
        }

        // ----------------------------------------------------------------
        // TESTS D'UNICITE
        // excludeId permet d'ignorer le vehicule en cours de modification.
        // ----------------------------------------------------------------

        public async Task<bool> ExistsImmatriculationAsync(string immatriculation, int? excludeId = null)
            => await _context.Vehicules
                .AnyAsync(v => v.Immatriculation == immatriculation
                               && (excludeId == null || v.Id != excludeId));

        public async Task<bool> ExistsTrackerGpsIdAsync(string trackerGpsId, int? excludeId = null)
            => await _context.Vehicules
                .AnyAsync(v => v.TrackerGpsId == trackerGpsId
                               && (excludeId == null || v.Id != excludeId));

        public async Task<bool> ExistsVINAsync(string vin, int? excludeId = null)
            => await _context.Vehicules
                .AnyAsync(v => v.VIN == vin
                               && (excludeId == null || v.Id != excludeId));
    }
}
