namespace Geofarmland.Server.Domain.Entities
{
    public class SensorData
    {
        public int Id { get; set; }
        public required string SensorType { get; set; }
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }
}
