namespace Geofarmland.Server.Domain.Entities;

public class Farm : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public required string Name { get; set; }
    public string? Code { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<LandParcel> LandParcels { get; } = new List<LandParcel>();
}
