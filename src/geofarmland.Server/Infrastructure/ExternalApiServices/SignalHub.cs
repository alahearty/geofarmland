using Microsoft.AspNetCore.SignalR;

namespace Geofarmland.Server.Infrastructure.ExternalApiServices
{
    public class SignalHub : Hub
    {
        public async Task SendMessage(string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", message);
        }

        public async Task SendSensorData(object sensorData)
        {
            await Clients.All.SendAsync("ReceiveSensorData", sensorData);
        }

        public async Task SendWeatherData(object weatherData)
        {
            await Clients.All.SendAsync("ReceiveWeatherData", weatherData);
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
