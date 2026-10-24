using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.ExternalApiServices;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Infrastructure.Repositories;
using Geofarmland.Server.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.RealTimeMonitoring.GetRealTimeMonitoring
{
    public class GetIoTSensorData : Endpoint<GetIoTSensorData.Request, GetIoTSensorData.MyResponse>
    {
        private readonly SensorDataRepository _repository;
        private readonly IHubContext<SignalHub> _hubContext;
        private readonly ApplicationDbContext _db;

        public GetIoTSensorData(SensorDataRepository repository, IHubContext<SignalHub> hubContext, ApplicationDbContext db)
        {
            _repository = repository;
            _hubContext = hubContext;
            _db = db;
        }

        public class Request
        {
            public string? DeviceId { get; set; }
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
            Policies("Authenticated");
            Throttle(60, 60);
        }

        public override async Task HandleAsync(Request req, CancellationToken ct)
        {
            var ownerId = CurrentUser.Id(User);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                await SendUnauthorizedAsync(ct);
                return;
            }

            var deviceId = req.DeviceId?.Trim() ?? string.Empty;
            if (deviceId.Length is < 1 or > 80)
            {
                ThrowError("DeviceId is required and must be 80 characters or fewer.");
                return;
            }
            if (!double.IsFinite(req.Latitude) || req.Latitude is < -90 or > 90
                || !double.IsFinite(req.Longitude) || req.Longitude is < -180 or > 180)
            {
                ThrowError("Latitude must be between -90 and 90 and longitude between -180 and 180.");
                return;
            }
            if (!double.IsFinite(req.Rainfall) || req.Rainfall is < 0 or > 10_000)
            {
                ThrowError("Rainfall must be between 0 and 10,000 mm.");
                return;
            }
            if (!double.IsFinite(req.SoilMoisture) || req.SoilMoisture is < 0 or > 100)
            {
                ThrowError("Soil moisture must be between 0 and 100 percent.");
                return;
            }

            var device = await _db.Devices.SingleOrDefaultAsync(d => d.OwnerId == ownerId && d.DeviceId == deviceId, ct);
            if (device == null)
            {
                ThrowError("Register this device before sending sensor readings.");
                return;
            }

            var sensorData = new SensorData
            {
                DeviceId = deviceId,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                Rainfall = req.Rainfall,
                SoilMoisture = req.SoilMoisture,
                Timestamp = DateTime.UtcNow,
                OwnerId = ownerId
            };

            await _repository.AddAsync(sensorData);

            var sensorDataPayload = new
            {
                sensorData.Id,
                sensorData.DeviceId,
                sensorData.Latitude,
                sensorData.Longitude,
                sensorData.Rainfall,
                sensorData.SoilMoisture,
                sensorData.Timestamp
            };

            // Broadcast only to the authenticated owner of that reading.
            await _hubContext.Clients.User(ownerId).SendAsync("ReceiveSensorData", sensorDataPayload, ct);

            await SendAsync(new MyResponse
            {
                Message = "IoT data received and broadcasted.",
                SensorDataId = sensorData.Id
            }, cancellation: ct);
        }
    }
}

