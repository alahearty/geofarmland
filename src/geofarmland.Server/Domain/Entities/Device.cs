namespace Geofarmland.Server.Domain.Entities
{
    public class Device : BaseEntity
    {
        public required string DeviceId { get; set; }
        public required string Name { get; set; }
        public int? PlotId { get; set; }
        public Plot? Plot { get; set; }
        public int? LandParcelId { get; set; }
        public LandParcel? LandParcel { get; set; }
        public string? OwnerId { get; set; }
        public ApplicationUser? Owner { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
