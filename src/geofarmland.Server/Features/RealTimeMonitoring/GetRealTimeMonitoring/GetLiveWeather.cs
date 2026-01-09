using FastEndpoints;
using Geofarmland.Server.Infrastructure.ExternalApiServices;
using Microsoft.AspNetCore.SignalR;

namespace Geofarmland.Server.Features.RealTimeMonitoring.GetRealTimeMonitoring
{
    public class GetLiveWeatherRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class GetLiveWeatherResponse
    {
        public required string Temperature { get; set; }
        public required string Humidity { get; set; }
        public required string Condition { get; set; }
        public required string Location { get; set; }
    }

    public class GetLiveWeather : Endpoint<GetLiveWeatherRequest, GetLiveWeatherResponse>
    {
        private readonly WeatherApiService _weatherApiService;
        private readonly IHubContext<SignalHub> _hubContext;

        public GetLiveWeather(WeatherApiService weatherApiService, IHubContext<SignalHub> hubContext)
        {
            _weatherApiService = weatherApiService;
            _hubContext = hubContext;
        }

        public override void Configure()
        {
            Get("/weather/live");
            AllowAnonymous();
        }

        public override async Task HandleAsync(GetLiveWeatherRequest req, CancellationToken ct)
        {
            try
            {
                var weatherResponse = await _weatherApiService.GetWeatherDataObjectAsync(req.Latitude, req.Longitude);

                if (weatherResponse == null || weatherResponse.Main == null || weatherResponse.Weather == null || weatherResponse.Weather.Count == 0)
                {
                    await SendNotFoundAsync(ct);
                    return;
                }

                var response = new GetLiveWeatherResponse
                {
                    Temperature = $"{weatherResponse.Main.Temp}°C",
                    Humidity = $"{weatherResponse.Main.Humidity}%",
                    Condition = weatherResponse.Weather[0].Main ?? "Unknown",
                    Location = weatherResponse.Name ?? "Unknown"
                };

                // Broadcast weather data via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveWeatherData", response, ct);

                await SendAsync(response, cancellation: ct);
            }
            catch (InvalidOperationException)
            {
                await SendAsync(new GetLiveWeatherResponse
                {
                    Temperature = "N/A",
                    Humidity = "N/A",
                    Condition = "API key not configured",
                    Location = "N/A"
                }, statusCode: 503, cancellation: ct);
            }
            catch (Exception)
            {
                await SendErrorsAsync(500, ct);
            }
        }
    }
}
