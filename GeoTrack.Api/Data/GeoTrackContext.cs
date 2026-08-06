using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GeoTrack.Api.Models;

namespace GeoTrack.Api.Data
{
    /// <summary>
    /// GEO-18 : le contexte herite desormais d'IdentityDbContext pour porter,
    /// en plus des donnees metier, les tables Identity (AspNetUsers, AspNetRoles...).
    /// </summary>
    public class GeoTrackContext : IdentityDbContext<ApplicationUser>
    {
        public GeoTrackContext(DbContextOptions<GeoTrackContext> options) : base(options)
        {
        }

        public DbSet<PositionGps> PositionsGps { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Indispensable : configure le schema Identity avant nos propres entites.
            base.OnModelCreating(builder);
        }
    }
}
