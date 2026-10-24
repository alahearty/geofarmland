using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Inventory;

public class UpdateInventoryItemEndpoint : Endpoint<InventoryItemRequest, InventoryItemDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateInventoryItemEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/inventory/items/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(InventoryItemRequest req, CancellationToken ct)
    {
        var itemId = Route<int>("id");
        var item = await _db.InventoryItems.FirstOrDefaultAsync(entity => entity.Id == itemId, ct);
        if (item == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, item.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage inventory.", 403);
            return;
        }
        if (!InventoryValidation.TryNormalize(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        item.Name = values.Name;
        item.ItemType = values.ItemType;
        item.Unit = values.Unit;
        item.QuantityOnHand = values.QuantityOnHand;
        item.ReorderLevel = values.ReorderLevel;
        item.UnitCost = values.UnitCost;
        item.Supplier = values.Supplier;
        item.Notes = values.Notes;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await SendAsync(InventoryMapping.ToDto(item), cancellation: ct);
    }
}
