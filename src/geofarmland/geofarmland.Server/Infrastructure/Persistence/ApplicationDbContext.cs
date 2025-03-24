using geofarmland.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace geofarmland.Server.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Plot> Plots { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Plot>()
                .Property(p => p.Geometry)
                .HasColumnType("geometry");
        }
    }
}
