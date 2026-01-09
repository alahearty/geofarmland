namespace Geofarmland.Server.Domain.Entities
{
    public class Hydrology : BaseEntity
    {
        public required string LocationName { get; set; }
        public required string GeoJson { get; set; }  // Stores GeoJSON data
        public required double Rainfall { get; set; }  // mm
        public required double SoilMoisture { get; set; }  // Percentage
        public required DateTime RecordedAt { get; set; }
    }

}
