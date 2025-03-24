using System.Text.Json;

namespace Geofarmland.Server.Features.RealTimeMonitoring.GetRealTimeMonitoring
{
    public class GetLiveWeather
    {
        private readonly HttpClient _httpClient;

        public GetLiveWeather(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<WeatherData?> FetchWeatherAsync(string location)
        {
            var response = await _httpClient.GetAsync($"https://api.weather.com/v3/wx/conditions/current?apiKey=YOUR_API_KEY&format=json&language=en-US&location={location}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var weatherData = JsonSerializer.Deserialize<WeatherData>(content);

            return weatherData;
        }
    }

    public class WeatherData
    {
        public required string Temperature { get; set; }
        public required string Humidity { get; set; }
        public required string Condition { get; set; }
    }
}
