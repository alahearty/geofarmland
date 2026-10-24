using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Domain.Entities;

public class InventoryItem : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public required string Name { get; set; }
    public InventoryItemType ItemType { get; set; } = InventoryItemType.Input;
    public required string Unit { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Supplier { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
