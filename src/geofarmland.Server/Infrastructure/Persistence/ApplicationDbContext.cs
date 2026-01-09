using Geofarmland.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Plot> Plots { get; set; }
        public DbSet<Hydrology> Hydrologys { get; set; }
        public DbSet<SensorData> SensorData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Plot>()
                .Property(p => p.Geometry)
                .HasColumnType("geometry");
        }
    }
}
