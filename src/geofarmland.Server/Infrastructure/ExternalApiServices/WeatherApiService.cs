using System.Text.Json;
using System.Text.Json.Serialization;
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
            return await GetJsonAsync($"{BaseUrl}/weather?lat={lat}&lon={lon}&appid={ApiKey}&units=metric");
        }

        public async Task<WeatherResponse?> GetWeatherDataObjectAsync(double lat, double lon)
        {
            var json = await GetWeatherDataAsync(lat, lon);
            return JsonSerializer.Deserialize<WeatherResponse>(json, SerializerOptions);
        }

        public async Task<ForecastResponse?> GetForecastDataObjectAsync(double lat, double lon)
        {
            var json = await GetJsonAsync($"{BaseUrl}/forecast?lat={lat}&lon={lon}&appid={ApiKey}&units=metric");
            return JsonSerializer.Deserialize<ForecastResponse>(json, SerializerOptions);
        }

        private string ApiKey
        {
            get
            {
                var apiKey = _configuration["ExternalApis:OpenWeatherMap:ApiKey"];
                if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_OPENWEATHERMAP_API_KEY")
                {
                    throw new InvalidOperationException("OpenWeatherMap API key is not configured. Please set it in appsettings.json");
                }
                return apiKey;
            }
        }

        private string BaseUrl =>
            _configuration["ExternalApis:OpenWeatherMap:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private async Task<string> GetJsonAsync(string url)
        {
            try
            {
                return await _httpClient.GetStringAsync(url);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Failed to fetch weather data: {ex.Message}", ex);
            }
        }

        public class WeatherResponse
        {
            public Main? Main { get; set; }
            public List<Weather>? Weather { get; init; }
            public Wind? Wind { get; set; }
            public string? Name { get; set; }
        }

        public class ForecastResponse
        {
            public List<ForecastEntry>? List { get; init; }
            public City? City { get; set; }
        }

        public class ForecastEntry
        {
            [JsonPropertyName("dt")]
            public long Timestamp { get; set; }
            public Main? Main { get; set; }
            public List<Weather>? Weather { get; init; }
        }

        public class City
        {
            public string? Name { get; set; }
        }

        public class Main
        {
            public double Temp { get; set; }

            [JsonPropertyName("feels_like")]
            public double FeelsLike { get; set; }

            public double Humidity { get; set; }
            public double Pressure { get; set; }
        }

        public class Wind
        {
            public double Speed { get; set; }
        }

        public class Weather
        {
            public string? Main { get; set; }
            public string? Description { get; set; }
            public string? Icon { get; set; }
        }
    }
}
