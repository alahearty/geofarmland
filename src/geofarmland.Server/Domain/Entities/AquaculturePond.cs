using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Domain.Entities;

public class AquaculturePond : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int FarmId { get; set; }
    public Farm? Farm { get; set; }
    public int? LandParcelId { get; set; }
    public LandParcel? LandParcel { get; set; }
    public required string Name { get; set; }
    public WaterType WaterType { get; set; } = WaterType.Freshwater;
    public AquacultureSystem SystemType { get; set; } = AquacultureSystem.Pond;
    public decimal AreaM2 { get; set; }
    public string? Species { get; set; }
    public int StockCount { get; set; }
    public decimal? AverageWeightG { get; set; }
    public DateOnly? LastStockedDate { get; set; }
    public AquacultureStatus Status { get; set; } = AquacultureStatus.Active;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
