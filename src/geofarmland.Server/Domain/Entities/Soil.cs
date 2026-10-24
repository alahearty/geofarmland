using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Domain.Entities;

public class SoilSample : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int FarmId { get; set; }
    public Farm? Farm { get; set; }
    public int LandParcelId { get; set; }
    public LandParcel? LandParcel { get; set; }
    public DateOnly SampleDate { get; set; }
    public decimal PH { get; set; }
    public decimal OrganicMatterPct { get; set; }
    public decimal? NitrogenPpm { get; set; }
    public decimal? PhosphorusPpm { get; set; }
    public decimal? PotassiumPpm { get; set; }
    public string? Laboratory { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class IrrigationEvent : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int FarmId { get; set; }
    public Farm? Farm { get; set; }
    public int LandParcelId { get; set; }
    public LandParcel? LandParcel { get; set; }
    public DateOnly EventDate { get; set; }
    public decimal VolumeM3 { get; set; }
    public int? DurationMinutes { get; set; }
    public IrrigationSource Source { get; set; } = IrrigationSource.Borehole;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
