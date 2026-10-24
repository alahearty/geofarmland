using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.ExternalApiServices;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.RealTimeMonitoring.GetRealTimeMonitoring
{
    public class GetLiveWeatherRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int? PlotId { get; set; }
    }

    public class GetLiveWeatherResponse
    {
        public required string Temperature { get; set; }
        public required string FeelsLike { get; set; }
        public required string Humidity { get; set; }
        public required string Pressure { get; set; }
        public required string WindSpeed { get; set; }
        public required string Condition { get; set; }
        public required string Description { get; set; }
        public required string Icon { get; set; }
        public required string Location { get; set; }
        public DateTime FetchedAtUtc { get; set; }
    }

    public class GetLiveWeather : Endpoint<GetLiveWeatherRequest, GetLiveWeatherResponse>
    {
        private readonly WeatherApiService _weatherApiService;
        private readonly IHubContext<SignalHub> _hubContext;
        private readonly ILogger<GetLiveWeather> _logger;
        private readonly ApplicationDbContext _db;

        public GetLiveWeather(
            WeatherApiService weatherApiService,
            IHubContext<SignalHub> hubContext,
            ILogger<GetLiveWeather> logger,
            ApplicationDbContext db)
        {
            _weatherApiService = weatherApiService;
            _hubContext = hubContext;
            _logger = logger;
            _db = db;
        }

        public override void Configure()
        {
            Get("/weather/live");
            Policies("Authenticated");
            Throttle(30, 60);
        }

        public override async Task HandleAsync(GetLiveWeatherRequest req, CancellationToken ct)
        {
            var userId = CurrentUser.Id(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                await SendUnauthorizedAsync(ct);
                return;
            }

            if (!double.IsFinite(req.Latitude) || req.Latitude is < -90 or > 90
                || !double.IsFinite(req.Longitude) || req.Longitude is < -180 or > 180)
            {
                ThrowError("Latitude must be between -90 and 90 and longitude between -180 and 180.");
                return;
            }

            if (req.PlotId.HasValue)
            {
                var canUsePlot = CurrentUser.IsAdmin(User)
                    ? await _db.Plots.AnyAsync(p => p.Id == req.PlotId.Value, ct)
                    : await _db.Plots.AnyAsync(p => p.Id == req.PlotId.Value && p.OwnerId == userId, ct);
                if (!canUsePlot)
                {
                    ThrowError("The selected plot does not belong to you.");
                    return;
                }
            }

            try
            {
                var weatherResponse = await _weatherApiService.GetWeatherDataObjectAsync(req.Latitude, req.Longitude);

                if (weatherResponse?.Main == null || weatherResponse.Weather == null || weatherResponse.Weather.Count == 0)
                {
                    await SendNotFoundAsync(ct);
                    return;
                }

                var fetchedAt = DateTime.UtcNow;
                var current = weatherResponse.Weather[0];
                var response = new GetLiveWeatherResponse
                {
                    Temperature = $"{weatherResponse.Main.Temp:0.#}°C",
                    FeelsLike = $"{weatherResponse.Main.FeelsLike:0.#}°C",
                    Humidity = $"{weatherResponse.Main.Humidity:0.#}%",
                    Pressure = $"{weatherResponse.Main.Pressure:0} hPa",
                    WindSpeed = $"{weatherResponse.Wind?.Speed ?? 0:0.#} m/s",
                    Condition = current.Main ?? "Unknown",
                    Description = current.Description ?? current.Main ?? "Unknown",
                    Icon = current.Icon ?? string.Empty,
                    Location = weatherResponse.Name ?? "Unknown",
                    FetchedAtUtc = fetchedAt
                };

                _db.WeatherSnapshots.Add(new WeatherSnapshot
                {
                    OwnerId = userId,
                    PlotId = req.PlotId,
                    Latitude = req.Latitude,
                    Longitude = req.Longitude,
                    TemperatureC = weatherResponse.Main.Temp,
                    FeelsLikeC = weatherResponse.Main.FeelsLike,
                    Humidity = weatherResponse.Main.Humidity,
                    Pressure = weatherResponse.Main.Pressure,
                    WindSpeed = weatherResponse.Wind?.Speed ?? 0,
                    Condition = response.Condition,
                    Description = response.Description,
                    Location = response.Location,
                    FetchedAtUtc = fetchedAt
                });
                await _db.SaveChangesAsync(ct);

                await _hubContext.Clients.User(userId).SendAsync("ReceiveWeatherData", response, ct);
                await SendAsync(response, cancellation: ct);
            }
            catch (InvalidOperationException)
            {
                await SendAsync(new GetLiveWeatherResponse
                {
                    Temperature = "N/A",
                    FeelsLike = "N/A",
                    Humidity = "N/A",
                    Pressure = "N/A",
                    WindSpeed = "N/A",
                    Condition = "API key not configured",
                    Description = "API key not configured",
                    Icon = string.Empty,
                    Location = "N/A"
                }, statusCode: 503, cancellation: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Live weather lookup failed for {Latitude},{Longitude}", req.Latitude, req.Longitude);
                await SendErrorsAsync(500, ct);
            }
        }
    }
}
