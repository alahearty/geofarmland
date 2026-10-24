using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Geofarmland.Server.Tests;

public class GeofarmlandApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=geofarmland_test;Username=postgres;Password=worisoft@123",
                ["Jwt:Key"] = "GeoFarmland-SuperSecret-Key-Min32Chars!",
                ["Jwt:Issuer"] = "Geofarmland.Server",
                ["Jwt:Audience"] = "Geofarmland.Client",
                ["ExternalApis:OpenWeatherMap:ApiKey"] = "YOUR_OPENWEATHERMAP_API_KEY"
            });
        });
    }
}

[TestFixture]
[NonParallelizable]
public class GeofarmlandApiTests : IDisposable
{
    private static int _testIpCounter;

    private GeofarmlandApiFactory _factory = null!;
    private HttpClient _client = null!;

    private static string NextTestIp() =>
        $"127.0.1.{Interlocked.Increment(ref _testIpCounter) % 250 + 1}";

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new GeofarmlandApiFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        // TestServer does not populate a remote IP, which FastEndpoints throttling uses as its key.
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", NextTestIp());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [SetUp]
    public void SetUp()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        _client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", NextTestIp());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Plots.RemoveRange(db.Plots.ToList());
        db.SensorData.RemoveRange(db.SensorData.ToList());
        db.Devices.RemoveRange(db.Devices.ToList());
        db.WeatherSnapshots.RemoveRange(db.WeatherSnapshots.ToList());
        db.HarvestRecords.RemoveRange(db.HarvestRecords.ToList());
        db.CropSeasons.RemoveRange(db.CropSeasons.ToList());
        db.Crops.RemoveRange(db.Crops.ToList());
        db.LivestockEvents.RemoveRange(db.LivestockEvents.ToList());
        db.LivestockGroups.RemoveRange(db.LivestockGroups.ToList());
        db.FarmTasks.RemoveRange(db.FarmTasks.ToList());
        db.SoilSamples.RemoveRange(db.SoilSamples.ToList());
        db.IrrigationEvents.RemoveRange(db.IrrigationEvents.ToList());
        db.InventoryItems.RemoveRange(db.InventoryItems.ToList());
        db.FinancialRecords.RemoveRange(db.FinancialRecords.ToList());
        db.AquaculturePonds.RemoveRange(db.AquaculturePonds.ToList());
        db.GreenhouseUnits.RemoveRange(db.GreenhouseUnits.ToList());
        db.RefreshTokens.RemoveRange(db.RefreshTokens.ToList());

        var disposableOrganizations = db.Organizations
            .Where(organization => organization.Slug != DataSeeder.DemoOrganizationSlug)
            .Select(organization => organization.Id)
            .ToList();
        if (disposableOrganizations.Count > 0)
        {
            db.LandParcels.RemoveRange(db.LandParcels.Where(parcel => disposableOrganizations.Contains(parcel.OrganizationId)));
            db.Farms.RemoveRange(db.Farms.Where(farm => disposableOrganizations.Contains(farm.OrganizationId)));
            db.OrganizationMembers.RemoveRange(db.OrganizationMembers.Where(member => disposableOrganizations.Contains(member.OrganizationId)));
            db.Organizations.RemoveRange(db.Organizations.Where(organization => disposableOrganizations.Contains(organization.Id)));
        }
        db.SaveChanges();
    }

    private Task<string> LoginAsync(HttpClient client) =>
        LoginAsync(client, "admin@geofarmland.com", "Admin@123456");

    private async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });

        var loginBody = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), loginBody);
        using var document = JsonDocument.Parse(loginBody);
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static HttpClient WithToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient NewClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", NextTestIp());
        return client;
    }

    private static List<List<double>> OpenRing()
    {
        return new List<List<double>>
        {
            new() { 18.855, -33.930 },
            new() { 18.865, -33.930 },
            new() { 18.865, -33.936 },
            new() { 18.855, -33.936 }
        };
    }

    [Test]
    public async Task ProtectedEndpointRequiresJwt()
    {
        var response = await _client.GetAsync("/plots");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task LoginRejectsInvalidPasswordAndAcceptsSeededAdmin()
    {
        var invalid = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@geofarmland.com",
            password = "wrong"
        });
        Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var token = await LoginAsync(_client);
        Assert.That(token, Is.Not.Empty);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await _client.SendAsync(request);
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task PlotCrudWorksAndClosesOpenRings()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);

        var create = await client.PostAsJsonAsync("/plots", new
        {
            name = "Test Vineyard",
            coordinates = OpenRing()
        });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var createBody = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var plotId = createBody.RootElement.GetProperty("plotId").GetInt32();
        Assert.That(plotId, Is.GreaterThan(0));

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/plots");
        Assert.That(list, Is.Not.Null);
        Assert.That(list!, Has.Count.EqualTo(1));
        Assert.That(list![0].GetProperty("name").GetString(), Is.EqualTo("Test Vineyard"));
        var firstCoordinate = list![0].GetProperty("coordinates")[0];
        Assert.That(firstCoordinate[0].GetDouble(), Is.EqualTo(18.855)); // longitude first
        Assert.That(firstCoordinate[1].GetDouble(), Is.EqualTo(-33.930)); // latitude second

        var updateRing = new List<List<double>>
        {
            new() { 18.856, -33.931 },
            new() { 18.864, -33.931 },
            new() { 18.864, -33.935 },
            new() { 18.856, -33.935 }
        };
        var update = await client.PutAsJsonAsync($"/plots/{plotId}", new
        {
            name = "Updated Vineyard",
            coordinates = updateRing
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updated = await client.GetFromJsonAsync<List<JsonElement>>("/plots");
        Assert.That(updated![0].GetProperty("name").GetString(), Is.EqualTo("Updated Vineyard"));
        Assert.That(updated[0].GetProperty("coordinates").GetArrayLength(), Is.EqualTo(5));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = db.Plots.Find(plotId)!;
            Assert.That(stored.OwnerId, Is.Not.Null.And.Not.Empty);
            Assert.That(stored.Geometry.SRID, Is.EqualTo(4326));
            Assert.That(stored.Geometry.IsValid, Is.True);
        }

        var delete = await client.DeleteAsync($"/plots/{plotId}");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var after = await client.GetFromJsonAsync<List<JsonElement>>("/plots");
        Assert.That(after!, Is.Empty);
    }

    [Test]
    public async Task PlotValidationRejectsTooFewPoints()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);

        var response = await client.PostAsJsonAsync("/plots", new
        {
            name = "Too small",
            coordinates = new List<List<double>>
            {
                new() { 18.85, -33.93 },
                new() { 18.86, -33.93 }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PlotValidationRejectsOutOfRangeAndSelfIntersectingPolygons()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);

        var outOfRange = await client.PostAsJsonAsync("/plots", new
        {
            name = "Out of range",
            coordinates = new List<List<double>>
            {
                new() { 18.85, 95 },
                new() { 18.86, -33.93 },
                new() { 18.87, -33.94 }
            }
        });
        Assert.That(outOfRange.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var selfIntersecting = await client.PostAsJsonAsync("/plots", new
        {
            name = "Bow tie",
            coordinates = new List<List<double>>
            {
                new() { 18.80, -33.90 },
                new() { 18.90, -33.94 },
                new() { 18.80, -33.94 },
                new() { 18.90, -33.90 }
            }
        });
        Assert.That(selfIntersecting.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task SensorIngestionAndHistoryWork()
    {
        Assert.That((await _client.GetAsync("/iot/sensor-data")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);
        var device = await client.PostAsJsonAsync("/devices", new
        {
            deviceId = "test-sensor",
            name = "Test sensor"
        });
        Assert.That(device.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ingest = await client.PostAsJsonAsync("/iot/sensor-data", new
        {
            deviceId = "test-sensor",
            latitude = -33.93,
            longitude = 18.86,
            rainfall = 1.5,
            soilMoisture = 22.5
        });
        Assert.That(ingest.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var readings = await client.GetFromJsonAsync<List<JsonElement>>("/iot/sensor-data");
        Assert.That(readings, Is.Not.Null);
        Assert.That(readings!, Has.Count.EqualTo(1));
        Assert.That(readings![0].GetProperty("deviceId").GetString(), Is.EqualTo("test-sensor"));
        Assert.That(readings![0].GetProperty("soilMoisture").GetDouble(), Is.EqualTo(22.5));
        Assert.That(readings![0].GetProperty("rainfall").GetDouble(), Is.EqualTo(1.5));
        Assert.That(readings![0].GetProperty("latitude").GetDouble(), Is.EqualTo(-33.93));
        Assert.That(readings![0].GetProperty("longitude").GetDouble(), Is.EqualTo(18.86));
    }

    [Test]
    public async Task WeatherRequiresAuthAndReturnsControlledErrorWithoutApiKey()
    {
        Assert.That((await _client.GetAsync("/weather/live?latitude=-33.93&longitude=18.86")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);
        var response = await client.GetAsync("/weather/live?latitude=-33.93&longitude=18.86");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task OwnershipSeparatesPlotsAndSensorReadings()
    {
        var adminToken = await LoginAsync(_client);
        var admin = WithToken(_client, adminToken);

        var create = await admin.PostAsJsonAsync("/plots", new
        {
            name = "Admin plot",
            coordinates = OpenRing()
        });
        create.EnsureSuccessStatusCode();
        using var createBody = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var adminPlotId = createBody.RootElement.GetProperty("plotId").GetInt32();

        var adminDevice = await admin.PostAsJsonAsync("/devices", new
        {
            deviceId = "admin-sensor",
            name = "Admin sensor",
            plotId = adminPlotId
        });
        Assert.That(adminDevice.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await admin.PostAsJsonAsync("/iot/sensor-data", new
        {
            deviceId = "admin-sensor",
            latitude = -33.93,
            longitude = 18.86,
            rainfall = 0,
            soilMoisture = 12
        });

        var email = $"ownership.{Guid.NewGuid():N}@test.local";
        var registration = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Strong1!x",
            fullName = "Ownership Test"
        });
        Assert.That(registration.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var otherToken = await LoginAsync(_client, email, "Strong1!x");
        var other = WithToken(NewClient(), otherToken);
        try
        {
            var visiblePlots = await other.GetFromJsonAsync<List<JsonElement>>("/plots");
            Assert.That(visiblePlots, Is.Empty);
            Assert.That((await other.GetAsync($"/plots/{adminPlotId}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await other.DeleteAsync($"/plots/{adminPlotId}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            var visibleSensors = await other.GetFromJsonAsync<List<JsonElement>>("/iot/sensor-data");
            Assert.That(visibleSensors, Is.Empty);

            var visibleDevices = await other.GetFromJsonAsync<List<JsonElement>>("/devices");
            Assert.That(visibleDevices, Is.Empty);

            var otherPlot = await other.PostAsJsonAsync("/plots", new
            {
                name = "Owned by the second user",
                coordinates = OpenRing()
            });
            Assert.That(otherPlot.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var allPlots = await admin.GetFromJsonAsync<List<JsonElement>>("/plots");
            Assert.That(allPlots, Has.Count.EqualTo(2));
        }
        finally
        {
            other.Dispose();
        }
    }

    [Test]
    public async Task RefreshRotationAndLogoutRevokeTokens()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@geofarmland.com",
            password = "Admin@123456"
        });
        login.EnsureSuccessStatusCode();
        using var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var access = loginBody.RootElement.GetProperty("accessToken").GetString()!;
        var refresh = loginBody.RootElement.GetProperty("refreshToken").GetString()!;

        var rotate = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });
        Assert.That(rotate.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var rotateBody = JsonDocument.Parse(await rotate.Content.ReadAsStringAsync());
        var rotatedAccess = rotateBody.RootElement.GetProperty("accessToken").GetString()!;
        var rotatedRefresh = rotateBody.RootElement.GetProperty("refreshToken").GetString()!;

        var reused = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });
        Assert.That(reused.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new { refreshToken = rotatedRefresh })
        };
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rotatedAccess);
        Assert.That((await _client.SendAsync(logout)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var afterLogout = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotatedRefresh });
        Assert.That(afterLogout.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        _ = access;
    }

    [Test]
    public async Task DeviceRegistrationCrudAndAdminOverviewWork()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);

        var create = await client.PostAsJsonAsync("/devices", new
        {
            deviceId = "device-crud",
            name = "Device CRUD"
        });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var deviceId = body.RootElement.GetProperty("id").GetInt32();

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/devices");
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list![0].GetProperty("deviceId").GetString(), Is.EqualTo("device-crud"));

        var update = await client.PutAsJsonAsync($"/devices/{deviceId}", new
        {
            deviceId = "device-crud-updated",
            name = "Device CRUD updated"
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/admin/overview");
        Assert.That(overview.GetProperty("devices").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("users").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("organizations").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("farms").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("landParcels").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("recentUsers").GetArrayLength(), Is.GreaterThanOrEqualTo(1));

        var profile = await client.PutAsJsonAsync("/api/auth/me", new { fullName = "System Administrator" });
        Assert.That(profile.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var history = await client.GetFromJsonAsync<List<JsonElement>>("/weather/history");
        Assert.That(history, Is.Empty);
        Assert.That((await client.GetAsync("/weather/forecast?latitude=-33.93&longitude=18.86")).StatusCode,
            Is.EqualTo(HttpStatusCode.ServiceUnavailable));

        var delete = await client.DeleteAsync($"/devices/{deviceId}");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task EnterpriseContextOverviewAndFarmParcelCrudWork()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);

        var context = await client.GetFromJsonAsync<JsonElement>("/api/enterprise/context");
        Assert.That(context.GetProperty("activeOrganization").GetProperty("name").GetString(),
            Is.EqualTo(DataSeeder.DemoOrganizationName));
        Assert.That(context.GetProperty("activeOrganization").GetProperty("role").GetString(),
            Is.EqualTo("OrganizationAdministrator"));
        Assert.That(context.GetProperty("canManageFarms").GetBoolean(), Is.True);
        Assert.That(context.GetProperty("canManageParcels").GetBoolean(), Is.True);

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/enterprise/overview");
        Assert.That(overview.GetProperty("metrics").GetProperty("farms").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("metrics").GetProperty("parcels").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("metrics").GetProperty("hectares").GetDecimal(), Is.GreaterThan(0m));

        var code = $"TEST-{Guid.NewGuid():N}"[..12];
        var createFarm = await client.PostAsJsonAsync("/api/farms", new
        {
            name = "Integration Farm",
            code,
            region = "Western Cape",
            description = "Created by the enterprise integration test."
        });
        Assert.That(createFarm.StatusCode, Is.EqualTo(HttpStatusCode.OK), await createFarm.Content.ReadAsStringAsync());
        using var farmBody = JsonDocument.Parse(await createFarm.Content.ReadAsStringAsync());
        var farmId = farmBody.RootElement.GetProperty("id").GetInt32();

        var createParcel = await client.PostAsJsonAsync("/api/land-parcels", new
        {
            farmId,
            name = "North Orchard",
            code = $"{code}-P1",
            landUse = "Orchard",
            tenureType = "Owned",
            status = "Active",
            coordinates = new List<List<double>>
            {
                new() { 19.000, -34.000 },
                new() { 19.010, -34.000 },
                new() { 19.010, -34.006 },
                new() { 19.000, -34.006 }
            }
        });
        Assert.That(createParcel.StatusCode, Is.EqualTo(HttpStatusCode.OK), await createParcel.Content.ReadAsStringAsync());
        using var parcelBody = JsonDocument.Parse(await createParcel.Content.ReadAsStringAsync());
        var parcelId = parcelBody.RootElement.GetProperty("id").GetInt32();
        Assert.That(parcelBody.RootElement.GetProperty("areaHectares").GetDecimal(), Is.GreaterThan(0m));
        Assert.That(parcelBody.RootElement.GetProperty("farmName").GetString(), Is.EqualTo("Integration Farm"));
        Assert.That(parcelBody.RootElement.GetProperty("coordinates").GetArrayLength(), Is.EqualTo(5));

        var parcels = await client.GetFromJsonAsync<JsonElement>(
            $"/api/land-parcels?farmId={farmId}&search=orchard");
        Assert.That(parcels.GetProperty("total").GetInt32(), Is.EqualTo(1));
        Assert.That(parcels.GetProperty("items")[0].GetProperty("landUse").GetString(), Is.EqualTo("Orchard"));

        var updateParcel = await client.PutAsJsonAsync($"/api/land-parcels/{parcelId}", new
        {
            farmId,
            name = "North Orchard Updated",
            code = $"{code}-P1",
            landUse = "Vineyard",
            tenureType = "Leased",
            status = "UnderDevelopment"
        });
        Assert.That(updateParcel.StatusCode, Is.EqualTo(HttpStatusCode.OK), await updateParcel.Content.ReadAsStringAsync());
        using var updatedBody = JsonDocument.Parse(await updateParcel.Content.ReadAsStringAsync());
        Assert.That(updatedBody.RootElement.GetProperty("name").GetString(), Is.EqualTo("North Orchard Updated"));
        Assert.That(updatedBody.RootElement.GetProperty("landUse").GetString(), Is.EqualTo("Vineyard"));
        Assert.That(updatedBody.RootElement.GetProperty("status").GetString(), Is.EqualTo("UnderDevelopment"));

        Assert.That((await client.DeleteAsync($"/api/land-parcels/{parcelId}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await client.DeleteAsync($"/api/farms/{farmId}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task CropSeasonAndHarvestWorkflowUpdatesProductionMetrics()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);

        var farms = await client.GetFromJsonAsync<List<JsonElement>>("/api/farms");
        var parcels = await client.GetFromJsonAsync<JsonElement>("/api/land-parcels?pageSize=10");
        Assert.That(farms, Is.Not.Null.And.Not.Empty);
        var farmId = farms![0].GetProperty("id").GetInt32();
        var parcelId = parcels.GetProperty("items")[0].GetProperty("id").GetInt32();

        var cropName = $"Integration Crop {Guid.NewGuid():N}"[..28];
        var createCrop = await client.PostAsJsonAsync("/api/crops", new
        {
            name = cropName,
            category = "Cereal",
            variety = "Test variety",
            description = "Created by the crop integration test."
        });
        Assert.That(createCrop.StatusCode, Is.EqualTo(HttpStatusCode.OK), await createCrop.Content.ReadAsStringAsync());
        using var cropBody = JsonDocument.Parse(await createCrop.Content.ReadAsStringAsync());
        var cropId = cropBody.RootElement.GetProperty("id").GetInt32();

        var createSeason = await client.PostAsJsonAsync("/api/crop-seasons", new
        {
            cropId,
            farmId,
            landParcelId = parcelId,
            seasonName = "Integration Season",
            status = "Growing",
            plantingDate = "2026-01-15",
            expectedHarvestDate = "2026-08-15",
            areaHectares = 1.5,
            expectedYieldTons = 7.5,
            notes = "Created by the crop integration test."
        });
        Assert.That(createSeason.StatusCode, Is.EqualTo(HttpStatusCode.OK), await createSeason.Content.ReadAsStringAsync());
        using var seasonBody = JsonDocument.Parse(await createSeason.Content.ReadAsStringAsync());
        var seasonId = seasonBody.RootElement.GetProperty("id").GetInt32();
        Assert.That(seasonBody.RootElement.GetProperty("cropName").GetString(), Is.EqualTo(cropName));
        Assert.That(seasonBody.RootElement.GetProperty("harvestedTons").GetDecimal(), Is.EqualTo(0m));

        var harvest = await client.PostAsJsonAsync($"/api/crop-seasons/{seasonId}/harvests", new
        {
            harvestDate = "2026-06-20",
            quantityTons = 3.25,
            grade = "A",
            notes = "First harvest block"
        });
        Assert.That(harvest.StatusCode, Is.EqualTo(HttpStatusCode.OK), await harvest.Content.ReadAsStringAsync());
        using var harvestBody = JsonDocument.Parse(await harvest.Content.ReadAsStringAsync());
        Assert.That(harvestBody.RootElement.GetProperty("quantityTons").GetDecimal(), Is.EqualTo(3.25m));
        Assert.That(harvestBody.RootElement.GetProperty("cropName").GetString(), Is.EqualTo(cropName));

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/crops/overview");
        Assert.That(overview.GetProperty("metrics").GetProperty("activeSeasons").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("metrics").GetProperty("harvestedTons").GetDecimal(), Is.GreaterThanOrEqualTo(3.25m));
        var seasonSummary = overview.GetProperty("seasons")
            .EnumerateArray()
            .First(item => item.GetProperty("id").GetInt32() == seasonId);
        Assert.That(seasonSummary.GetProperty("harvestedTons").GetDecimal(), Is.EqualTo(3.25m));

        var analytics = await client.GetFromJsonAsync<JsonElement>("/api/analytics/overview");
        Assert.That(analytics.GetProperty("metrics").GetProperty("harvestedTons").GetDecimal(), Is.GreaterThanOrEqualTo(3.25m));
        Assert.That(analytics.GetProperty("monthlyHarvests").GetArrayLength(), Is.EqualTo(6));
        Assert.That(analytics.GetProperty("cropPerformance")
            .EnumerateArray()
            .Any(item => item.GetProperty("cropName").GetString() == cropName), Is.True);

        var updateSeason = await client.PutAsJsonAsync($"/api/crop-seasons/{seasonId}", new
        {
            cropId,
            farmId,
            landParcelId = parcelId,
            seasonName = "Integration Season",
            status = "Harvested",
            plantingDate = "2026-01-15",
            expectedHarvestDate = "2026-08-15",
            areaHectares = 1.5,
            expectedYieldTons = 7.5
        });
        Assert.That(updateSeason.StatusCode, Is.EqualTo(HttpStatusCode.OK), await updateSeason.Content.ReadAsStringAsync());

        Assert.That((await client.DeleteAsync($"/api/crop-seasons/{seasonId}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await client.DeleteAsync($"/api/crops/{cropId}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task AquacultureAndGreenhouseRegistersExposeProductionMetrics()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);
        var farms = await client.GetFromJsonAsync<List<JsonElement>>("/api/farms");
        Assert.That(farms, Is.Not.Null);
        var farmId = farms![0].GetProperty("id").GetInt32();

        var pond = await client.PostAsJsonAsync("/api/aquaculture/ponds", new
        {
            farmId,
            name = "Integration Pond",
            waterType = "Freshwater",
            systemType = "Pond",
            areaM2 = 850.5,
            species = "Tilapia",
            stockCount = 1200,
            averageWeightG = 320,
            lastStockedDate = "2026-09-15",
            status = "Active",
            notes = "Created by aquaculture integration test."
        });
        Assert.That(pond.StatusCode, Is.EqualTo(HttpStatusCode.OK), await pond.Content.ReadAsStringAsync());
        using var pondBody = JsonDocument.Parse(await pond.Content.ReadAsStringAsync());
        var pondId = pondBody.RootElement.GetProperty("id").GetInt32();
        Assert.That(pondBody.RootElement.GetProperty("stockCount").GetInt32(), Is.EqualTo(1200));

        var greenhouse = await client.PostAsJsonAsync("/api/greenhouses/units", new
        {
            farmId,
            name = "Integration Tunnel",
            structureType = "Tunnel",
            areaM2 = 480,
            currentCrop = "Tomatoes",
            status = "Active",
            notes = "Created by greenhouse integration test."
        });
        Assert.That(greenhouse.StatusCode, Is.EqualTo(HttpStatusCode.OK), await greenhouse.Content.ReadAsStringAsync());
        using var greenhouseBody = JsonDocument.Parse(await greenhouse.Content.ReadAsStringAsync());
        var greenhouseId = greenhouseBody.RootElement.GetProperty("id").GetInt32();

        var aquaculture = await client.GetFromJsonAsync<JsonElement>("/api/aquaculture/overview");
        Assert.That(aquaculture.GetProperty("metrics").GetProperty("ponds").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(aquaculture.GetProperty("metrics").GetProperty("totalStock").GetInt32(), Is.GreaterThanOrEqualTo(1200));
        Assert.That(aquaculture.GetProperty("metrics").GetProperty("waterAreaM2").GetDecimal(), Is.GreaterThanOrEqualTo(850.5m));

        var greenhouses = await client.GetFromJsonAsync<JsonElement>("/api/greenhouses/overview");
        Assert.That(greenhouses.GetProperty("metrics").GetProperty("units").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(greenhouses.GetProperty("metrics").GetProperty("coveredAreaM2").GetDecimal(), Is.GreaterThanOrEqualTo(480m));
        Assert.That(greenhouses.GetProperty("metrics").GetProperty("croppedUnits").GetInt32(), Is.GreaterThanOrEqualTo(1));

        Assert.That((await client.DeleteAsync($"/api/aquaculture/ponds/{pondId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await client.DeleteAsync($"/api/greenhouses/units/{greenhouseId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task InventoryItemsTrackLowStockAndValue()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);
        var create = await client.PostAsJsonAsync("/api/inventory/items", new
        {
            name = "Integration fertiliser",
            itemType = "Input",
            unit = "bag",
            quantityOnHand = 4,
            reorderLevel = 5,
            unitCost = 250,
            supplier = "Integration supplier",
            notes = "Created by inventory integration test."
        });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.OK), await create.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var itemId = body.RootElement.GetProperty("id").GetInt32();
        Assert.That(body.RootElement.GetProperty("isLowStock").GetBoolean(), Is.True);
        Assert.That(body.RootElement.GetProperty("stockValue").GetDecimal(), Is.EqualTo(1000m));

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/inventory/overview");
        Assert.That(overview.GetProperty("metrics").GetProperty("items").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("metrics").GetProperty("lowStock").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("metrics").GetProperty("stockValue").GetDecimal(), Is.GreaterThanOrEqualTo(1000m));

        var update = await client.PutAsJsonAsync($"/api/inventory/items/{itemId}", new
        {
            name = "Integration fertiliser",
            itemType = "Input",
            unit = "bag",
            quantityOnHand = 12,
            reorderLevel = 5,
            unitCost = 250
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK), await update.Content.ReadAsStringAsync());
        using var updated = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.That(updated.RootElement.GetProperty("isLowStock").GetBoolean(), Is.False);

        Assert.That((await client.DeleteAsync($"/api/inventory/items/{itemId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task FinanceRecordsCalculateRevenueExpenseAndNetPosition()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);

        var revenue = await client.PostAsJsonAsync("/api/finance/records", new
        {
            recordType = "Revenue",
            category = "Integration produce sales",
            amount = 12500.50,
            recordDate = "2026-10-10",
            description = "Integration revenue"
        });
        Assert.That(revenue.StatusCode, Is.EqualTo(HttpStatusCode.OK), await revenue.Content.ReadAsStringAsync());
        using var revenueBody = JsonDocument.Parse(await revenue.Content.ReadAsStringAsync());
        var revenueId = revenueBody.RootElement.GetProperty("id").GetInt32();

        var expense = await client.PostAsJsonAsync("/api/finance/records", new
        {
            recordType = "Expense",
            category = "Integration inputs",
            amount = 4200.25,
            recordDate = "2026-10-12",
            description = "Integration expense"
        });
        Assert.That(expense.StatusCode, Is.EqualTo(HttpStatusCode.OK), await expense.Content.ReadAsStringAsync());
        using var expenseBody = JsonDocument.Parse(await expense.Content.ReadAsStringAsync());
        var expenseId = expenseBody.RootElement.GetProperty("id").GetInt32();

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/finance/overview");
        Assert.That(overview.GetProperty("metrics").GetProperty("revenue").GetDecimal(), Is.GreaterThanOrEqualTo(12500.50m));
        Assert.That(overview.GetProperty("metrics").GetProperty("expenses").GetDecimal(), Is.GreaterThanOrEqualTo(4200.25m));
        Assert.That(overview.GetProperty("metrics").GetProperty("netPosition").GetDecimal(), Is.EqualTo(
            overview.GetProperty("metrics").GetProperty("revenue").GetDecimal()
            - overview.GetProperty("metrics").GetProperty("expenses").GetDecimal()));
        Assert.That(overview.GetProperty("records").GetArrayLength(), Is.GreaterThanOrEqualTo(2));

        Assert.That((await client.DeleteAsync($"/api/finance/records/{revenueId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await client.DeleteAsync($"/api/finance/records/{expenseId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task SoilSamplesAndIrrigationUpdateSoilMetrics()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);
        var farms = await client.GetFromJsonAsync<List<JsonElement>>("/api/farms");
        var parcels = await client.GetFromJsonAsync<JsonElement>("/api/land-parcels?pageSize=10");
        Assert.That(farms, Is.Not.Null);
        var farmId = farms![0].GetProperty("id").GetInt32();
        var parcelId = parcels.GetProperty("items")[0].GetProperty("id").GetInt32();

        var sample = await client.PostAsJsonAsync("/api/soil/samples", new
        {
            farmId,
            landParcelId = parcelId,
            sampleDate = "2026-10-01",
            ph = 6.5,
            organicMatterPct = 2.8,
            nitrogenPpm = 15,
            phosphorusPpm = 10,
            potassiumPpm = 100,
            laboratory = "Integration Lab",
            notes = "Created by soil integration test."
        });
        Assert.That(sample.StatusCode, Is.EqualTo(HttpStatusCode.OK), await sample.Content.ReadAsStringAsync());
        using var sampleBody = JsonDocument.Parse(await sample.Content.ReadAsStringAsync());
        var sampleId = sampleBody.RootElement.GetProperty("id").GetInt32();

        var irrigation = await client.PostAsJsonAsync("/api/soil/irrigation", new
        {
            farmId,
            landParcelId = parcelId,
            eventDate = "2026-10-15",
            volumeM3 = 120.5,
            durationMinutes = 90,
            source = "Borehole",
            notes = "Integration irrigation event"
        });
        Assert.That(irrigation.StatusCode, Is.EqualTo(HttpStatusCode.OK), await irrigation.Content.ReadAsStringAsync());
        using var irrigationBody = JsonDocument.Parse(await irrigation.Content.ReadAsStringAsync());
        var irrigationId = irrigationBody.RootElement.GetProperty("id").GetInt32();

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/soil/overview");
        Assert.That(overview.GetProperty("metrics").GetProperty("samples").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("metrics").GetProperty("averagePH").GetDecimal(), Is.GreaterThan(0m));
        Assert.That(overview.GetProperty("metrics").GetProperty("irrigationEventsLast30Days").GetInt32(),
            Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("metrics").GetProperty("waterVolumeLast30DaysM3").GetDecimal(),
            Is.GreaterThanOrEqualTo(120.5m));
        Assert.That(overview.GetProperty("samples").GetArrayLength(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("irrigationEvents").GetArrayLength(), Is.GreaterThanOrEqualTo(1));

        Assert.That((await client.DeleteAsync($"/api/soil/samples/{sampleId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await client.DeleteAsync($"/api/soil/irrigation/{irrigationId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task FieldTaskWorkflowTracksOverdueAndCompletion()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);
        var farms = await client.GetFromJsonAsync<List<JsonElement>>("/api/farms");
        Assert.That(farms, Is.Not.Null);
        var farmId = farms![0].GetProperty("id").GetInt32();

        var create = await client.PostAsJsonAsync("/api/operations/tasks", new
        {
            farmId,
            title = "Integration irrigation inspection",
            taskType = "Inspection",
            priority = "High",
            status = "Pending",
            dueDate = "2026-10-01",
            notes = "Created by the field operations integration test."
        });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.OK), await create.Content.ReadAsStringAsync());
        using var taskBody = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var taskId = taskBody.RootElement.GetProperty("id").GetInt32();
        Assert.That(taskBody.RootElement.GetProperty("isOverdue").GetBoolean(), Is.True);

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/operations/overview");
        Assert.That(overview.GetProperty("metrics").GetProperty("overdue").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("metrics").GetProperty("highPriority").GetInt32(), Is.GreaterThanOrEqualTo(1));

        var complete = await client.PostAsync($"/api/operations/tasks/{taskId}/complete", null);
        Assert.That(complete.StatusCode, Is.EqualTo(HttpStatusCode.OK), await complete.Content.ReadAsStringAsync());
        using var completedBody = JsonDocument.Parse(await complete.Content.ReadAsStringAsync());
        Assert.That(completedBody.RootElement.GetProperty("status").GetString(), Is.EqualTo("Completed"));
        Assert.That(completedBody.RootElement.GetProperty("completedAt").ValueKind, Is.Not.EqualTo(JsonValueKind.Null));

        var after = await client.GetFromJsonAsync<JsonElement>("/api/operations/overview");
        Assert.That(after.GetProperty("metrics").GetProperty("completedLast30Days").GetInt32(), Is.GreaterThanOrEqualTo(1));

        Assert.That((await client.DeleteAsync($"/api/operations/tasks/{taskId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task IotOverviewLinksDevicesToParcelsAndReportsTelemetry()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);
        var parcels = await client.GetFromJsonAsync<JsonElement>("/api/land-parcels?pageSize=10");
        var parcel = parcels.GetProperty("items")[0];
        var parcelId = parcel.GetProperty("id").GetInt32();
        var parcelName = parcel.GetProperty("name").GetString();

        var deviceId = $"iot-{Guid.NewGuid():N}"[..16];
        var create = await client.PostAsJsonAsync("/devices", new
        {
            deviceId,
            name = "Integration IoT probe",
            landParcelId = parcelId
        });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.OK), await create.Content.ReadAsStringAsync());
        using var deviceBody = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var internalDeviceId = deviceBody.RootElement.GetProperty("id").GetInt32();
        Assert.That(deviceBody.RootElement.GetProperty("landParcelId").GetInt32(), Is.EqualTo(parcelId));
        Assert.That(deviceBody.RootElement.GetProperty("landParcelName").GetString(), Is.EqualTo(parcelName));

        var ingest = await client.PostAsJsonAsync("/iot/sensor-data", new
        {
            deviceId,
            latitude = -33.93,
            longitude = 18.86,
            rainfall = 2.25,
            soilMoisture = 31.5
        });
        Assert.That(ingest.StatusCode, Is.EqualTo(HttpStatusCode.OK), await ingest.Content.ReadAsStringAsync());

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/iot/overview");
        Assert.That(overview.GetProperty("metrics").GetProperty("devices").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(overview.GetProperty("metrics").GetProperty("readingsLast24Hours").GetInt32(), Is.GreaterThanOrEqualTo(1));
        var device = overview.GetProperty("devices")
            .EnumerateArray()
            .First(item => item.GetProperty("id").GetInt32() == internalDeviceId);
        Assert.That(device.GetProperty("landParcelId").GetInt32(), Is.EqualTo(parcelId));
        Assert.That(device.GetProperty("reportingLast24h").GetBoolean(), Is.True);
        Assert.That(device.GetProperty("lastSoilMoisture").GetDouble(), Is.EqualTo(31.5));
        Assert.That(device.GetProperty("lastRainfall").GetDouble(), Is.EqualTo(2.25));

        await client.DeleteAsync($"/devices/{internalDeviceId}");
    }

    [Test]
    public async Task LivestockGroupAndEventsUpdateHeadCountAndWeight()
    {
        var token = await LoginAsync(_client);
        var client = WithToken(_client, token);
        var farms = await client.GetFromJsonAsync<List<JsonElement>>("/api/farms");
        var parcels = await client.GetFromJsonAsync<JsonElement>("/api/land-parcels?pageSize=10");
        Assert.That(farms, Is.Not.Null);
        var farmId = farms![0].GetProperty("id").GetInt32();
        var parcelId = parcels.GetProperty("items")[0].GetProperty("id").GetInt32();

        var groupName = $"Integration Herd {Guid.NewGuid():N}"[..24];
        var createGroup = await client.PostAsJsonAsync("/api/livestock/groups", new
        {
            farmId,
            grazingLandParcelId = parcelId,
            name = groupName,
            species = "Cattle",
            breed = "Bonsmara",
            purpose = "Beef",
            headCount = 10,
            averageWeightKg = 450,
            status = "Active",
            joinedDate = "2026-01-10",
            notes = "Created by the livestock integration test."
        });
        Assert.That(createGroup.StatusCode, Is.EqualTo(HttpStatusCode.OK), await createGroup.Content.ReadAsStringAsync());
        using var groupBody = JsonDocument.Parse(await createGroup.Content.ReadAsStringAsync());
        var groupId = groupBody.RootElement.GetProperty("id").GetInt32();
        Assert.That(groupBody.RootElement.GetProperty("headCount").GetInt32(), Is.EqualTo(10));

        var weightEvent = await client.PostAsJsonAsync($"/api/livestock/groups/{groupId}/events", new
        {
            eventType = "Weight",
            eventDate = "2026-10-15",
            weightKg = 487.5,
            notes = "Routine weighing"
        });
        Assert.That(weightEvent.StatusCode, Is.EqualTo(HttpStatusCode.OK), await weightEvent.Content.ReadAsStringAsync());

        var mortalityEvent = await client.PostAsJsonAsync($"/api/livestock/groups/{groupId}/events", new
        {
            eventType = "Mortality",
            eventDate = "2026-10-20",
            headCount = 2,
            notes = "Two animals lost"
        });
        Assert.That(mortalityEvent.StatusCode, Is.EqualTo(HttpStatusCode.OK), await mortalityEvent.Content.ReadAsStringAsync());

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/livestock/overview");
        Assert.That(overview.GetProperty("metrics").GetProperty("totalHead").GetInt32(), Is.GreaterThanOrEqualTo(8));
        var groupSummary = overview.GetProperty("groups")
            .EnumerateArray()
            .First(item => item.GetProperty("id").GetInt32() == groupId);
        Assert.That(groupSummary.GetProperty("headCount").GetInt32(), Is.EqualTo(8));
        Assert.That(groupSummary.GetProperty("averageWeightKg").GetDecimal(), Is.EqualTo(487.5m));
        Assert.That(groupSummary.GetProperty("eventCount").GetInt32(), Is.EqualTo(2));
        Assert.That(overview.GetProperty("recentEvents").GetArrayLength(), Is.GreaterThanOrEqualTo(2));

        var analytics = await client.GetFromJsonAsync<JsonElement>("/api/analytics/overview");
        Assert.That(analytics.GetProperty("metrics").GetProperty("livestockHead").GetInt32(), Is.GreaterThanOrEqualTo(8));
        Assert.That(analytics.GetProperty("species")
            .EnumerateArray()
            .Any(item => item.GetProperty("species").GetString() == "Cattle"), Is.True);

        Assert.That((await client.DeleteAsync($"/api/livestock/groups/{groupId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task FarmerRoleCanReadButCannotMutateTheEnterpriseRegister()
    {
        var farmerToken = await LoginAsync(_client, "farmer@geofarmland.com", "Farmer@123456");
        var farmer = WithToken(NewClient(), farmerToken);
        try
        {
            var context = await farmer.GetFromJsonAsync<JsonElement>("/api/enterprise/context");
            Assert.That(context.GetProperty("activeOrganization").GetProperty("role").GetString(), Is.EqualTo("Farmer"));
            Assert.That(context.GetProperty("canManageFarms").GetBoolean(), Is.False);
            Assert.That(context.GetProperty("canManageParcels").GetBoolean(), Is.False);

            var farms = await farmer.GetFromJsonAsync<List<JsonElement>>("/api/farms");
            Assert.That(farms, Is.Not.Null);
            Assert.That(farms!, Has.Count.GreaterThanOrEqualTo(1));

            var createFarm = await farmer.PostAsJsonAsync("/api/farms", new
            {
                name = "Unauthorized Farm",
                code = $"NO-{Guid.NewGuid():N}"[..10]
            });
            Assert.That(createFarm.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

            var createParcel = await farmer.PostAsJsonAsync("/api/land-parcels", new
            {
                farmId = farms![0].GetProperty("id").GetInt32(),
                name = "Unauthorized Parcel",
                landUse = "Cropland",
                coordinates = OpenRing()
            });
            Assert.That(createParcel.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }
        finally
        {
            farmer.Dispose();
        }
    }

    [Test]
    public async Task EnterpriseOrganizationIsolationHidesDemoWorkspace()
    {
        var adminToken = await LoginAsync(_client);
        var admin = WithToken(NewClient(), adminToken);
        try
        {
            var adminContext = await admin.GetFromJsonAsync<JsonElement>("/api/enterprise/context");
            var demoOrganizationId = adminContext.GetProperty("activeOrganization").GetProperty("id").GetInt32();

            var email = $"enterprise.{Guid.NewGuid():N}@test.local";
            var registration = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Strong1!x",
                fullName = "Enterprise Isolated User"
            });
            Assert.That(registration.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var otherToken = await LoginAsync(_client, email, "Strong1!x");
            var other = WithToken(NewClient(), otherToken);
            try
            {
                var context = await other.GetFromJsonAsync<JsonElement>("/api/enterprise/context");
                Assert.That(context.GetProperty("activeOrganization").GetProperty("id").GetInt32(),
                    Is.Not.EqualTo(demoOrganizationId));
                Assert.That(context.GetProperty("activeOrganization").GetProperty("role").GetString(),
                    Is.EqualTo("OrganizationAdministrator"));

                var overview = await other.GetFromJsonAsync<JsonElement>("/api/enterprise/overview");
                Assert.That(overview.GetProperty("metrics").GetProperty("farms").GetInt32(), Is.EqualTo(0));
                Assert.That(overview.GetProperty("metrics").GetProperty("parcels").GetInt32(), Is.EqualTo(0));

                Assert.That((await other.GetAsync($"/api/farms?organizationId={demoOrganizationId}")).StatusCode,
                    Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That((await other.GetAsync($"/api/enterprise/overview?organizationId={demoOrganizationId}")).StatusCode,
                    Is.EqualTo(HttpStatusCode.NotFound));
            }
            finally
            {
                other.Dispose();
            }
        }
        finally
        {
            admin.Dispose();
        }
    }
}





