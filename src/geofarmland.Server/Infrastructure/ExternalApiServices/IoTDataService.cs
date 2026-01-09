namespace Geofarmland.Server.Infrastructure.ExternalApiServices
{
    public class IoTDataService
    {
        private readonly HttpClient _httpClient;

        public IoTDataService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetDataAsync(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<bool> SendDataAsync(string endpoint, HttpContent content)
        {
            var response = await _httpClient.PostAsync(endpoint, content);
            return response.IsSuccessStatusCode;
        }
    }
}
