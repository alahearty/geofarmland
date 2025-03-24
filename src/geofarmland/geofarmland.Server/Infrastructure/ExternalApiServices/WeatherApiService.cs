namespace Geofarmland.Server.Infrastructure.ExternalApiServices
{
    public class WeatherApiService
    {
        private readonly HttpClient _httpClient;

        public WeatherApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetWeatherDataAsync(double lat, double lon)
        {
            var apiKey = "YOUR_OPENWEATHERMAP_API_KEY";
            var url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={apiKey}&units=metric";

            var response = await _httpClient.GetStringAsync(url);
            return response;
        }
    }

}
