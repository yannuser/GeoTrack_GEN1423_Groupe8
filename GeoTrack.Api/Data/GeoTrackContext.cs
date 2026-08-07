using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GeoTrack.Api.Models;
using GeoTrack.Api.Services;

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

        // GEO-15 : l'entite Vehicule est declaree dans GeoTrack.Api.Services
        // (fichier GEO-15-VehiculeCrud-Service.cs). Elle est mappee telle quelle,
        // sans etre deplacee, pour ne pas toucher au contrat existant.
        public DbSet<Vehicule> Vehicules { get; set; }

        public DbSet<Conducteur> Conducteurs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Indispensable : configure le schema Identity avant nos propres entites.
            base.OnModelCreating(builder);

            ConfigurerVehicule(builder);
        }

        /// <summary>
        /// GEO-15 : les champs optionnels de Vehicule doivent etre declares
        /// explicitement nullables.
        ///
        /// Le projet active &lt;Nullable&gt;enable&lt;/Nullable&gt; : EF Core mappe donc
        /// par defaut tout <c>string</c> non-annote en colonne NOT NULL. Or
        /// VehiculeService affecte <c>request.VIN?.ToUpperInvariant()</c> et
        /// consorts, qui valent null quand le champ n'est pas fourni. Sans cette
        /// configuration, creer un vehicule sans VIN echouerait a l'insertion.
        /// Les champs marques [Required] par le contrat restent obligatoires.
        /// </summary>
        private static void ConfigurerVehicule(ModelBuilder builder)
        {
            var vehicule = builder.Entity<Vehicule>();

            vehicule.Property(v => v.VIN).IsRequired(false);
            vehicule.Property(v => v.FournisseurGps).IsRequired(false);
            vehicule.Property(v => v.ConducteurNom).IsRequired(false);
            vehicule.Property(v => v.GroupeDivision).IsRequired(false);
            vehicule.Property(v => v.Notes).IsRequired(false);
            vehicule.Property(v => v.CreePar).IsRequired(false);

            // Les regles d'unicite sont deja verifiees en amont par
            // VehiculeService (ExistsImmatriculationAsync / ExistsTrackerGpsIdAsync
            // / ExistsVINAsync). Ces index les adossent au schema : ils garantissent
            // l'unicite meme en cas d'ecritures concurrentes, et accelerent les
            // recherches par immatriculation et par tracker.
            vehicule.HasIndex(v => v.Immatriculation).IsUnique();
            vehicule.HasIndex(v => v.TrackerGpsId).IsUnique();

            // VIN est optionnel : le filtre evite que plusieurs vehicules sans VIN
            // soient consideres comme des doublons par SQL Server.
            vehicule.HasIndex(v => v.VIN).IsUnique().HasFilter("[VIN] IS NOT NULL");
        }
    }
}
