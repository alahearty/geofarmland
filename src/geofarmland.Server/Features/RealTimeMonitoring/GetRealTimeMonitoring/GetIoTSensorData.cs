using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Repositories;
using Geofarmland.Server.Infrastructure.ExternalApiServices;
using Microsoft.AspNetCore.SignalR;

namespace Geofarmland.Server.Features.RealTimeMonitoring.GetRealTimeMonitoring
{
    public class GetIoTSensorData : Endpoint<GetIoTSensorData.Request, GetIoTSensorData.MyResponse>
    {
        private readonly SensorDataRepository _repository;
        private readonly IHubContext<SignalHub> _hubContext;

        public GetIoTSensorData(SensorDataRepository repository, IHubContext<SignalHub> hubContext)
        {
            _repository = repository;
            _hubContext = hubContext;
        }

        public class Request
        {
            public required string DeviceId { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double Rainfall { get; set; }
            public double SoilMoisture { get; set; }
        }

        public class MyResponse
        {
            public required string Message { get; set; }
            public int SensorDataId { get; set; }
        }

        public override void Configure()
        {
            Post("/iot/sensor-data");
            AllowAnonymous();
        }

        public override async Task HandleAsync(Request req, CancellationToken ct)
        {
            var sensorData = new SensorData
            {
                Id = 0,
                SensorType = $"Device-{req.DeviceId}",
                Timestamp = DateTime.UtcNow,
                Value = req.SoilMoisture
            };

            await _repository.AddAsync(sensorData);

            // Prepare sensor data object for SignalR broadcast
            var sensorDataPayload = new
            {
                Id = sensorData.Id,
                DeviceId = req.DeviceId,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                Rainfall = req.Rainfall,
                SoilMoisture = req.SoilMoisture,
                Timestamp = sensorData.Timestamp,
                SensorType = sensorData.SensorType
            };

            // Broadcast sensor data via SignalR to all connected clients
            await _hubContext.Clients.All.SendAsync("ReceiveSensorData", sensorDataPayload, ct);

            await SendAsync(new MyResponse 
            { 
                Message = "IoT data received and broadcasted.",
                SensorDataId = sensorData.Id
            }, cancellation: ct);
        }
    }

}
