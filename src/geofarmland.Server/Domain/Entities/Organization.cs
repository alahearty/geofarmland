using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Domain.Entities;

public class Organization : BaseEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Country { get; set; } = "South Africa";
    public string CurrencyCode { get; set; } = "ZAR";
    public string TimeZone { get; set; } = "Africa/Johannesburg";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OrganizationMember> Members { get; } = new List<OrganizationMember>();
    public ICollection<Farm> Farms { get; } = new List<Farm>();
    public ICollection<LandParcel> LandParcels { get; } = new List<LandParcel>();
}

public class OrganizationMember : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public OrganizationRole Role { get; set; } = OrganizationRole.Farmer;
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
