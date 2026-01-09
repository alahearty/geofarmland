using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Geofarmland.Server.Infrastructure.ExternalApiServices
{
    public class WeatherApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public WeatherApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> GetWeatherDataAsync(double lat, double lon)
        {
            var apiKey = _configuration["ExternalApis:OpenWeatherMap:ApiKey"];
            var baseUrl = _configuration["ExternalApis:OpenWeatherMap:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5";
            
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_OPENWEATHERMAP_API_KEY")
            {
                throw new InvalidOperationException("OpenWeatherMap API key is not configured. Please set it in appsettings.json");
            }

            var url = $"{baseUrl}/weather?lat={lat}&lon={lon}&appid={apiKey}&units=metric";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                return response;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Failed to fetch weather data: {ex.Message}", ex);
            }
        }

        public async Task<WeatherResponse?> GetWeatherDataObjectAsync(double lat, double lon)
        {
            var json = await GetWeatherDataAsync(lat, lon);
            return JsonSerializer.Deserialize<WeatherResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        public class WeatherResponse
        {
            public Main? Main { get; set; }
            public List<Weather>? Weather { get; init; }
            public string? Name { get; set; }
        }

        public class Main
        {
            public double Temp { get; set; }
            public double Humidity { get; set; }
            public double Pressure { get; set; }
        }

        public class Weather
        {
            public string? Main { get; set; }
            public string? Description { get; set; }
        }
    }
}
