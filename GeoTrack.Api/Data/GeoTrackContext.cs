using Microsoft.EntityFrameworkCore;
using GeoTrack.Api.Models;

namespace GeoTrack.Api.Data
{
    public class GeoTrackContext : DbContext
    {
        public GeoTrackContext(DbContextOptions<GeoTrackContext> options) : base(options)
        {
        }

        public DbSet<PositionGps> PositionsGps { get; set; }
    }
}