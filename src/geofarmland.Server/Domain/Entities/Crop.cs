using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Domain.Entities;

public class Crop : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public required string Name { get; set; }
    public CropCategory Category { get; set; } = CropCategory.Other;
    public string? Variety { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CropSeason> Seasons { get; } = new List<CropSeason>();
}

public class CropSeason : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int FarmId { get; set; }
    public Farm? Farm { get; set; }
    public int LandParcelId { get; set; }
    public LandParcel? LandParcel { get; set; }
    public int CropId { get; set; }
    public Crop? Crop { get; set; }
    public required string SeasonName { get; set; }
    public CropSeasonStatus Status { get; set; } = CropSeasonStatus.Planned;
    public DateOnly PlantingDate { get; set; }
    public DateOnly? ExpectedHarvestDate { get; set; }
    public DateOnly? ActualHarvestDate { get; set; }
    public decimal AreaHectares { get; set; }
    public decimal? ExpectedYieldTons { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<HarvestRecord> Harvests { get; } = new List<HarvestRecord>();
}

public class HarvestRecord : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int CropSeasonId { get; set; }
    public CropSeason? CropSeason { get; set; }
    public DateOnly HarvestDate { get; set; }
    public decimal QuantityTons { get; set; }
    public HarvestGrade? Grade { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
