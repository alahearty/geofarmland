using Geofarmland.Server.Domain.Entities;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Geofarmland.Server.Infrastructure.Persistence;

public static class DataSeeder
{
    public const string AdminEmail = "admin@geofarmland.com";
    public const string AdminPassword = "Admin@123456";
    public const string AdminRole = "Admin";
    public const string FarmerEmail = "farmer@geofarmland.com";
    public const string FarmerPassword = "Farmer@123456";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

        // Create Admin role if not exists
        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRole));
            logger.LogInformation("Created {Role} role", AdminRole);
        }

        // Seed admin user if not exists
        var adminUser = await userManager.FindByEmailAsync(AdminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, AdminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, AdminRole);
                logger.LogInformation("Seeded admin user: {Email}", AdminEmail);
            }
            else
            {
                logger.LogError("Failed to seed admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (adminUser != null)
        {
            if (!await userManager.IsInRoleAsync(adminUser, AdminRole))
                await userManager.AddToRoleAsync(adminUser, AdminRole);

            var orphanedPlots = await db.Plots
                .Where(p => p.OwnerId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.OwnerId, adminUser.Id));
            var orphanedSensors = await db.SensorData
                .Where(s => s.OwnerId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(sensor => sensor.OwnerId, adminUser.Id));

            if (orphanedPlots > 0 || orphanedSensors > 0)
                logger.LogInformation("Assigned {Plots} legacy plots and {Sensors} legacy sensor readings to the seeded admin.", orphanedPlots, orphanedSensors);
        }

        var farmer = await userManager.FindByEmailAsync(FarmerEmail);
        if (farmer == null)
        {
            farmer = new ApplicationUser
            {
                UserName = FarmerEmail,
                Email = FarmerEmail,
                EmailConfirmed = true,
                FullName = "Demo Farmer",
                CreatedAt = DateTime.UtcNow
            };
            var farmerResult = await userManager.CreateAsync(farmer, FarmerPassword);
            if (!farmerResult.Succeeded)
            {
                logger.LogError("Failed to seed demo farmer: {Errors}", string.Join(", ", farmerResult.Errors.Select(e => e.Description)));
                farmer = null;
            }
        }

        if (adminUser != null)
        {
            var adminPlot = await db.Plots.FirstOrDefaultAsync(p => p.OwnerId == adminUser.Id);
            await EnsureDemoDeviceAsync(db, adminUser.Id, "field-sensor-01", "Admin field sensor", adminPlot?.Id);
        }

        if (farmer != null)
        {
            var farmerPlot = await db.Plots.FirstOrDefaultAsync(p => p.OwnerId == farmer.Id && p.Name == "Farmer Demo Field");
            if (farmerPlot == null)
            {
                var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
                var coordinates = new[]
                {
                    new Coordinate(18.44, -33.93),
                    new Coordinate(18.46, -33.93),
                    new Coordinate(18.46, -33.94),
                    new Coordinate(18.44, -33.94),
                    new Coordinate(18.44, -33.93)
                };
                farmerPlot = new Plot
                {
                    Name = "Farmer Demo Field",
                    OwnerId = farmer.Id,
                    Geometry = factory.CreatePolygon(coordinates)
                };
                db.Plots.Add(farmerPlot);
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded demo farmer plot and account.");
            }

            await EnsureDemoDeviceAsync(db, farmer.Id, "field-sensor-02", "Farmer field sensor", farmerPlot.Id);
        }
    }

    private static async Task EnsureDemoDeviceAsync(ApplicationDbContext db, string ownerId, string deviceId, string name, int? plotId)
    {
        if (await db.Devices.AnyAsync(d => d.OwnerId == ownerId && d.DeviceId == deviceId)) return;
        db.Devices.Add(new Device
        {
            DeviceId = deviceId,
            Name = name,
            PlotId = plotId,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

