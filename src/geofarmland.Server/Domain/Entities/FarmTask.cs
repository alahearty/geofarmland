using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Domain.Entities;

public class FarmTask : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int FarmId { get; set; }
    public Farm? Farm { get; set; }
    public int? LandParcelId { get; set; }
    public LandParcel? LandParcel { get; set; }
    public required string Title { get; set; }
    public FarmTaskType TaskType { get; set; } = FarmTaskType.Inspection;
    public FarmTaskPriority Priority { get; set; } = FarmTaskPriority.Medium;
    public FarmTaskStatus Status { get; set; } = FarmTaskStatus.Pending;
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
