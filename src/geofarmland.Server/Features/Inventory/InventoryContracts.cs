using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Features.Inventory;

public class InventoryItemRequest
{
    public string? Name { get; set; }
    public string? ItemType { get; set; }
    public string? Unit { get; set; }
    public decimal? QuantityOnHand { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Supplier { get; set; }
    public string? Notes { get; set; }
}

public sealed record InventoryItemDto(
    int Id,
    string Name,
    string ItemType,
    string Unit,
    decimal QuantityOnHand,
    decimal ReorderLevel,
    decimal? UnitCost,
    decimal? StockValue,
    bool IsLowStock,
    string? Supplier,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record InventoryMetricsDto(
    int Items,
    int LowStock,
    int Equipment,
    decimal StockValue);

public sealed record InventoryOverviewResponse(
    bool CanManage,
    InventoryMetricsDto Metrics,
    IReadOnlyList<InventoryItemDto> Items);

public static class InventoryMapping
{
    public static InventoryItemDto ToDto(InventoryItem item) => new(
        item.Id,
        item.Name,
        item.ItemType.ToString(),
        item.Unit,
        item.QuantityOnHand,
        item.ReorderLevel,
        item.UnitCost,
        item.UnitCost.HasValue ? Math.Round(item.QuantityOnHand * item.UnitCost.Value, 2) : null,
        item.QuantityOnHand <= item.ReorderLevel,
        item.Supplier,
        item.Notes,
        item.CreatedAt,
        item.UpdatedAt);
}

internal readonly record struct InventoryValues(
    string Name,
    InventoryItemType ItemType,
    string Unit,
    decimal QuantityOnHand,
    decimal ReorderLevel,
    decimal? UnitCost,
    string? Supplier,
    string? Notes);

internal static class InventoryValidation
{
    public static bool TryNormalize(InventoryItemRequest req, out InventoryValues values, out string error)
    {
        values = default;
        error = string.Empty;

        var name = req.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 160)
        {
            error = "An inventory item name between 2 and 160 characters is required.";
            return false;
        }

        var itemType = InventoryItemType.Input;
        if (!string.IsNullOrWhiteSpace(req.ItemType) && !Enum.TryParse(req.ItemType, true, out itemType))
        {
            error = "Select a valid inventory item type.";
            return false;
        }

        var unit = req.Unit?.Trim() ?? string.Empty;
        if (unit.Length is < 1 or > 40)
        {
            error = "A stock unit between 1 and 40 characters is required.";
            return false;
        }

        var quantity = req.QuantityOnHand ?? 0m;
        if (quantity < 0m)
        {
            error = "Quantity on hand cannot be negative.";
            return false;
        }

        var reorder = req.ReorderLevel ?? 0m;
        if (reorder < 0m)
        {
            error = "Reorder level cannot be negative.";
            return false;
        }

        if (req.UnitCost is < 0m)
        {
            error = "Unit cost cannot be negative.";
            return false;
        }

        var supplier = Clean(req.Supplier, 160, out var supplierValid);
        var notes = Clean(req.Notes, 1000, out var notesValid);
        if (!supplierValid || !notesValid)
        {
            error = "One or more inventory fields exceed the maximum length.";
            return false;
        }

        values = new InventoryValues(name, itemType, unit, quantity, reorder, req.UnitCost, supplier, notes);
        return true;
    }

    private static string? Clean(string? value, int maxLength, out bool valid)
    {
        var cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        valid = cleaned == null || cleaned.Length <= maxLength;
        return valid ? cleaned : null;
    }
}
