using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Domain.Entities;

public class GreenhouseUnit : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int FarmId { get; set; }
    public Farm? Farm { get; set; }
    public int? LandParcelId { get; set; }
    public LandParcel? LandParcel { get; set; }
    public required string Name { get; set; }
    public GreenhouseStructureType StructureType { get; set; } = GreenhouseStructureType.Greenhouse;
    public decimal AreaM2 { get; set; }
    public string? CurrentCrop { get; set; }
    public GreenhouseStatus Status { get; set; } = GreenhouseStatus.Active;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
