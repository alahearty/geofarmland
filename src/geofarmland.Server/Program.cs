using FastEndpoints;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Infrastructure.Repositories;
using Geofarmland.Server.Infrastructure.ExternalApiServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.UseNetTopologySuite()));

builder.Services.AddOpenApi();
builder.Services.AddFastEndpoints();

// SignalR configuration
builder.Services.AddSignalR();

// CORS configuration for SignalR and API access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
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

// Use CORS before other middleware
app.UseCors("AllowAll");

app.UseFastEndpoints();

app.UseDefaultFiles();
app.MapStaticAssets();

// Map SignalR Hub
app.MapHub<SignalHub>("/signalr/hub");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapFallbackToFile("/index.html");

await app.RunAsync();
