namespace Geofarmland.Server.Domain.Entities
{
    public class SensorData
    {
        public int Id { get; set; }
        public required string DeviceId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Rainfall { get; set; }
        public double SoilMoisture { get; set; }
        public DateTime Timestamp { get; set; }
        public string? OwnerId { get; set; }
        public ApplicationUser? Owner { get; set; }
    }
}
