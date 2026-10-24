using Geofarmland.Server.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Plot> Plots { get; set; }
    public DbSet<Hydrology> Hydrologys { get; set; }
    public DbSet<SensorData> SensorData { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<WeatherSnapshot> WeatherSnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Plot>()
            .Property(p => p.Geometry)
            .HasColumnType("geometry");

        builder.Entity<Plot>()
            .Property(p => p.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Entity<Plot>()
            .HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Plot>()
            .HasIndex(p => p.OwnerId);

        builder.Entity<SensorData>()
            .Property(p => p.DeviceId)
            .HasMaxLength(80)
            .IsRequired();

        builder.Entity<SensorData>()
            .HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SensorData>()
            .HasIndex(p => p.OwnerId);

        builder.Entity<RefreshToken>()
            .HasIndex(p => p.Token)
            .IsUnique();

        builder.Entity<Device>(b =>
        {
            b.Property(x => x.DeviceId).HasMaxLength(80).IsRequired();
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.HasIndex(x => new { x.OwnerId, x.DeviceId }).IsUnique();
            b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Plot).WithMany().HasForeignKey(x => x.PlotId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<WeatherSnapshot>(b =>
        {
            b.Property(x => x.Condition).HasMaxLength(120).IsRequired();
            b.Property(x => x.Description).HasMaxLength(240).IsRequired();
            b.Property(x => x.Location).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.OwnerId, x.FetchedAtUtc });
            b.HasIndex(x => new { x.PlotId, x.FetchedAtUtc });
            b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Plot).WithMany().HasForeignKey(x => x.PlotId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}

