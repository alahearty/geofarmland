namespace Geofarmland.Server.Domain.Entities
{
    public class WeatherSnapshot : BaseEntity
    {
        public string? OwnerId { get; set; }
        public ApplicationUser? Owner { get; set; }
        public int? PlotId { get; set; }
        public Plot? Plot { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double TemperatureC { get; set; }
        public double FeelsLikeC { get; set; }
        public double Humidity { get; set; }
        public double Pressure { get; set; }
        public double WindSpeed { get; set; }
        public required string Condition { get; set; }
        public required string Description { get; set; }
        public required string Location { get; set; }
        public DateTime FetchedAtUtc { get; set; }
    }
}
