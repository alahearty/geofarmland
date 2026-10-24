using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Domain.Entities;

public class LivestockGroup : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int FarmId { get; set; }
    public Farm? Farm { get; set; }
    public int? GrazingLandParcelId { get; set; }
    public LandParcel? GrazingLandParcel { get; set; }
    public required string Name { get; set; }
    public LivestockSpecies Species { get; set; } = LivestockSpecies.Cattle;
    public string? Breed { get; set; }
    public LivestockPurpose Purpose { get; set; } = LivestockPurpose.Beef;
    public int HeadCount { get; set; }
    public decimal? AverageWeightKg { get; set; }
    public LivestockStatus Status { get; set; } = LivestockStatus.Active;
    public DateOnly? JoinedDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<LivestockEvent> Events { get; } = new List<LivestockEvent>();
}

public class LivestockEvent : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int LivestockGroupId { get; set; }
    public LivestockGroup? LivestockGroup { get; set; }
    public LivestockEventType EventType { get; set; } = LivestockEventType.Health;
    public DateOnly EventDate { get; set; }
    public int? HeadCount { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
