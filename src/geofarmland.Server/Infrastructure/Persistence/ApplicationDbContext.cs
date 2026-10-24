using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;
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
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationMember> OrganizationMembers { get; set; }
    public DbSet<Farm> Farms { get; set; }
    public DbSet<LandParcel> LandParcels { get; set; }
    public DbSet<Crop> Crops { get; set; }
    public DbSet<CropSeason> CropSeasons { get; set; }
    public DbSet<HarvestRecord> HarvestRecords { get; set; }
    public DbSet<LivestockGroup> LivestockGroups { get; set; }
    public DbSet<LivestockEvent> LivestockEvents { get; set; }
    public DbSet<FarmTask> FarmTasks { get; set; }
    public DbSet<SoilSample> SoilSamples { get; set; }
    public DbSet<IrrigationEvent> IrrigationEvents { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<FinancialRecord> FinancialRecords { get; set; }
    public DbSet<AquaculturePond> AquaculturePonds { get; set; }
    public DbSet<GreenhouseUnit> GreenhouseUnits { get; set; }

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
            b.HasIndex(x => x.LandParcelId);
            b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Plot).WithMany().HasForeignKey(x => x.PlotId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.LandParcel).WithMany().HasForeignKey(x => x.LandParcelId).OnDelete(DeleteBehavior.SetNull);
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

        builder.Entity<Organization>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            b.Property(x => x.Country).HasMaxLength(80).IsRequired();
            b.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            b.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
            b.HasIndex(x => x.Slug).IsUnique();
            b.HasIndex(x => x.IsActive);
        });

        builder.Entity<OrganizationMember>(b =>
        {
            b.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
            b.HasIndex(x => x.UserId);
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
            b.HasOne(x => x.Organization).WithMany(x => x.Members).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Farm>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Region).HasMaxLength(120);
            b.Property(x => x.Country).HasMaxLength(80);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            b.HasOne(x => x.Organization).WithMany(x => x.Farms).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LandParcel>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.Geometry).HasColumnType("geometry");
            b.Property(x => x.AreaHectares).HasPrecision(18, 4);
            b.Property(x => x.LandUse).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.TenureType).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.HasIndex(x => new { x.OrganizationId, x.FarmId });
            b.HasIndex(x => x.LegacyPlotId);
            b.HasOne(x => x.Organization).WithMany(x => x.LandParcels).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Farm).WithMany(x => x.LandParcels).HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_LandParcels_Srid", "ST_SRID(\"Geometry\") = 4326");
                t.HasCheckConstraint("CK_LandParcels_ValidGeometry", "ST_IsValid(\"Geometry\")");
            });
        });

        builder.Entity<Crop>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Variety).HasMaxLength(120);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Category).HasConversion<string>().HasMaxLength(40);
            b.HasIndex(x => new { x.OrganizationId, x.Name });
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CropSeason>(b =>
        {
            b.Property(x => x.SeasonName).HasMaxLength(80).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.AreaHectares).HasPrecision(18, 4);
            b.Property(x => x.ExpectedYieldTons).HasPrecision(18, 2);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrganizationId, x.Status, x.PlantingDate });
            b.HasIndex(x => x.LandParcelId);
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LandParcel).WithMany().HasForeignKey(x => x.LandParcelId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Crop).WithMany(x => x.Seasons).HasForeignKey(x => x.CropId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HarvestRecord>(b =>
        {
            b.Property(x => x.QuantityTons).HasPrecision(18, 3);
            b.Property(x => x.Grade).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrganizationId, x.HarvestDate });
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CropSeason).WithMany(x => x.Harvests).HasForeignKey(x => x.CropSeasonId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LivestockGroup>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Breed).HasMaxLength(120);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.Species).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.AverageWeightKg).HasPrecision(18, 2);
            b.HasIndex(x => new { x.OrganizationId, x.Status });
            b.HasIndex(x => x.FarmId);
            b.HasIndex(x => x.GrazingLandParcelId);
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.GrazingLandParcel).WithMany().HasForeignKey(x => x.GrazingLandParcelId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<LivestockEvent>(b =>
        {
            b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.WeightKg).HasPrecision(18, 2);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrganizationId, x.EventDate });
            b.HasIndex(x => new { x.LivestockGroupId, x.EventDate });
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LivestockGroup).WithMany(x => x.Events).HasForeignKey(x => x.LivestockGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FarmTask>(b =>
        {
            b.Property(x => x.Title).HasMaxLength(160).IsRequired();
            b.Property(x => x.TaskType).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasIndex(x => new { x.OrganizationId, x.Status, x.DueDate });
            b.HasIndex(x => x.FarmId);
            b.HasIndex(x => x.LandParcelId);
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LandParcel).WithMany().HasForeignKey(x => x.LandParcelId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SoilSample>(b =>
        {
            b.Property(x => x.PH).HasPrecision(4, 2);
            b.Property(x => x.OrganicMatterPct).HasPrecision(5, 2);
            b.Property(x => x.NitrogenPpm).HasPrecision(10, 2);
            b.Property(x => x.PhosphorusPpm).HasPrecision(10, 2);
            b.Property(x => x.PotassiumPpm).HasPrecision(10, 2);
            b.Property(x => x.Laboratory).HasMaxLength(160);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrganizationId, x.SampleDate });
            b.HasIndex(x => x.LandParcelId);
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LandParcel).WithMany().HasForeignKey(x => x.LandParcelId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IrrigationEvent>(b =>
        {
            b.Property(x => x.VolumeM3).HasPrecision(18, 2);
            b.Property(x => x.Source).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrganizationId, x.EventDate });
            b.HasIndex(x => x.LandParcelId);
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LandParcel).WithMany().HasForeignKey(x => x.LandParcelId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryItem>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            b.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
            b.Property(x => x.ReorderLevel).HasPrecision(18, 3);
            b.Property(x => x.UnitCost).HasPrecision(18, 2);
            b.Property(x => x.Supplier).HasMaxLength(160);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrganizationId, x.ItemType });
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancialRecord>(b =>
        {
            b.Property(x => x.RecordType).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Category).HasMaxLength(80).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => new { x.OrganizationId, x.RecordDate });
            b.HasIndex(x => x.FarmId);
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AquaculturePond>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.WaterType).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.SystemType).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.AreaM2).HasPrecision(18, 2);
            b.Property(x => x.Species).HasMaxLength(120);
            b.Property(x => x.AverageWeightG).HasPrecision(18, 2);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrganizationId, x.Status });
            b.HasIndex(x => x.FarmId);
            b.HasIndex(x => x.LandParcelId);
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LandParcel).WithMany().HasForeignKey(x => x.LandParcelId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<GreenhouseUnit>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.StructureType).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.AreaM2).HasPrecision(18, 2);
            b.Property(x => x.CurrentCrop).HasMaxLength(120);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrganizationId, x.Status });
            b.HasIndex(x => x.FarmId);
            b.HasIndex(x => x.LandParcelId);
            b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LandParcel).WithMany().HasForeignKey(x => x.LandParcelId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}

