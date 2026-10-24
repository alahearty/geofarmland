using FastEndpoints;
using FastEndpoints.Security;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Infrastructure.Repositories;
using Geofarmland.Server.Infrastructure.ExternalApiServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Database & Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.UseNetTopologySuite()));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
// AddIdentity registers cookie authentication as the default scheme. The API must
// authenticate with JWT bearer tokens, otherwise protected endpoints redirect to
// /Account/Login even when a valid Authorization header is supplied.
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
});
builder.Services.AddAuthenticationJwtBearer(
    s => s.SigningKey = jwtKey,
    bearer =>
    {
        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/signalr/hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", x => x.RequireAuthenticatedUser());
    options.AddPolicy("Admin", x => x.RequireRole("Admin"));
});

builder.Services.AddOpenApi();
builder.Services.AddFastEndpoints();

// SignalR configuration
builder.Services.AddSignalR();

// CORS configuration for the Vite development server and same-origin production SPA.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length == 0)
{
    allowedOrigins =
    [
        "http://localhost:58036",
        "https://localhost:58035",
        "http://localhost:64210",
        "https://localhost:64210",
        "http://127.0.0.1:58036",
        "https://127.0.0.1:58035",
        "http://127.0.0.1:64210",
        "https://127.0.0.1:64210"
    ];
}
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register Repositories
builder.Services.AddScoped<PlotRepository>();
builder.Services.AddScoped<SensorDataRepository>();
builder.Services.AddScoped<HydrologyRepository>();

// Register External API Services with HttpClient
builder.Services.AddHttpClient<WeatherApiService>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["ExternalApis:OpenWeatherMap:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IoTDataService>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["ExternalApis:IoTService:BaseUrl"] ?? "http://localhost:5000/api";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Run migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(scope.ServiceProvider);
}

// Use CORS before other middleware
app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints();

// Serve the built Vue SPA when src/geofarmland.client/dist exists. This lets a
// student run the API and UI from one process with `dotnet run`.
var spaDistPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "geofarmland.client", "dist"));
var spaExists = Directory.Exists(spaDistPath);
var spaFileProvider = spaExists ? new PhysicalFileProvider(spaDistPath) : null;

if (spaFileProvider != null)
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = spaFileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = spaFileProvider });
}
else
{
    app.UseDefaultFiles();
}

app.MapStaticAssets();

// Map SignalR Hub
app.MapHub<SignalHub>("/signalr/hub");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

if (spaFileProvider != null)
{
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = spaFileProvider });
}
else
{
    app.MapFallbackToFile("/index.html");
}

await app.RunAsync();

// Exposes the top-level Program type to WebApplicationFactory-based tests.
public partial class Program { }
