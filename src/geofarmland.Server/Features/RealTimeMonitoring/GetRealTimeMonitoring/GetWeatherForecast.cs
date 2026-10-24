using FastEndpoints;
using Geofarmland.Server.Infrastructure.ExternalApiServices;

namespace Geofarmland.Server.Features.RealTimeMonitoring.GetRealTimeMonitoring;

public sealed record ForecastItemDto(
    DateTime TimeUtc,
    double TemperatureC,
    double FeelsLikeC,
    string Condition,
    string Description);

public class GetWeatherForecast : Endpoint<GetLiveWeatherRequest, List<ForecastItemDto>>
{
    private readonly WeatherApiService _weatherApiService;
    private readonly ILogger<GetWeatherForecast> _logger;

    public GetWeatherForecast(WeatherApiService weatherApiService, ILogger<GetWeatherForecast> logger)
    {
        _weatherApiService = weatherApiService;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/weather/forecast");
        Policies("Authenticated");
        Throttle(30, 60);
    }

    public override async Task HandleAsync(GetLiveWeatherRequest req, CancellationToken ct)
    {
        if (!double.IsFinite(req.Latitude) || req.Latitude is < -90 or > 90
            || !double.IsFinite(req.Longitude) || req.Longitude is < -180 or > 180)
        {
            ThrowError("Latitude must be between -90 and 90 and longitude between -180 and 180.");
            return;
        }

        try
        {
            var forecast = await _weatherApiService.GetForecastDataObjectAsync(req.Latitude, req.Longitude);
            if (forecast?.List == null || forecast.List.Count == 0)
            {
                await SendNotFoundAsync(ct);
                return;
            }

            var items = forecast.List
                .Take(8)
                .Select(entry => new ForecastItemDto(
                    DateTimeOffset.FromUnixTimeSeconds(entry.Timestamp).UtcDateTime,
                    entry.Main?.Temp ?? 0,
                    entry.Main?.FeelsLike ?? 0,
                    entry.Weather?.FirstOrDefault()?.Main ?? "Unknown",
                    entry.Weather?.FirstOrDefault()?.Description ?? "Unknown"))
                .ToList();

            await SendAsync(items, cancellation: ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 503);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weather forecast lookup failed for {Latitude},{Longitude}", req.Latitude, req.Longitude);
            await SendErrorsAsync(500, ct);
        }
    }
}
