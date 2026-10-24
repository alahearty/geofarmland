using FastEndpoints;
using Geofarmland.Server.Infrastructure.Repositories;
using Geofarmland.Server.Shared;

namespace Geofarmland.Server.Features.RealTimeMonitoring.GetRealTimeMonitoring
{
    public class GetRecentSensorData : EndpointWithoutRequest<List<SensorReadingResponse>>
    {
        private readonly SensorDataRepository _repository;

        public GetRecentSensorData(SensorDataRepository repository)
        {
            _repository = repository;
        }

        public override void Configure()
        {
            Get("/iot/sensor-data");
            Policies("Authenticated");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var ownerId = CurrentUser.Id(User);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                await SendUnauthorizedAsync(ct);
                return;
            }

            var isAdmin = CurrentUser.IsAdmin(User);
            var readings = await _repository.GetRecentAsync(ownerId, isAdmin, 20, ct);

            await SendAsync(readings
                .Select(reading => new SensorReadingResponse
                {
                    Id = reading.Id,
                    DeviceId = reading.DeviceId,
                    Latitude = reading.Latitude,
                    Longitude = reading.Longitude,
                    Rainfall = reading.Rainfall,
                    SoilMoisture = reading.SoilMoisture,
                    Timestamp = reading.Timestamp
                })
                .ToList(), cancellation: ct);
        }
    }

    public class SensorReadingResponse
    {
        public int Id { get; set; }
        public required string DeviceId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Rainfall { get; set; }
        public double SoilMoisture { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
