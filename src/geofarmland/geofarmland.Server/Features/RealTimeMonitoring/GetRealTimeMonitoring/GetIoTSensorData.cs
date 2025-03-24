using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Repositories;

namespace Geofarmland.Server.Features.RealTimeMonitoring.GetRealTimeMonitoring
{
    public class GetIoTSensorData : Endpoint<GetIoTSensorData.Request, GetIoTSensorData.MyResponse>
    {
        private readonly SensorDataRepository _repository;

        public GetIoTSensorData(SensorDataRepository repository)
        {
            _repository = repository;
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
                Value = req.SoilMoisture // Assuming SoilMoisture is the value to be stored
            };

            await _repository.AddAsync(sensorData);
            await SendAsync(new MyResponse { Message = "IoT data received." }, cancellation: ct);
        }
    }

}
