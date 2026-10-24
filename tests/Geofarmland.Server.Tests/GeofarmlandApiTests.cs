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
    private GeofarmlandApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new GeofarmlandApiFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        // TestServer does not populate a remote IP, which FastEndpoints throttling uses as its key.
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "127.0.0.1");
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
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Plots.RemoveRange(db.Plots.ToList());
        db.SensorData.RemoveRange(db.SensorData.ToList());
        db.Devices.RemoveRange(db.Devices.ToList());
        db.WeatherSnapshots.RemoveRange(db.WeatherSnapshots.ToList());
        db.RefreshTokens.RemoveRange(db.RefreshTokens.ToList());
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
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "127.0.0.1");
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
}





