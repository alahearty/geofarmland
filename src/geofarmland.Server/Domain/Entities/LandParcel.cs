using Geofarmland.Server.Domain.Enums;
using NetTopologySuite.Geometries;

namespace Geofarmland.Server.Domain.Entities;

public class LandParcel : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int FarmId { get; set; }
    public Farm? Farm { get; set; }
    public required string Name { get; set; }
    public string? Code { get; set; }
    public Polygon Geometry { get; set; } = null!;
    public decimal AreaHectares { get; set; }
    public LandUseType LandUse { get; set; } = LandUseType.Cropland;
    public TenureType TenureType { get; set; } = TenureType.Owned;
    public LandParcelStatus Status { get; set; } = LandParcelStatus.Active;
    public string? Notes { get; set; }
    public int? LegacyPlotId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
