using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;

namespace Geofarmland.Server.Features.Inventory;

public class CreateInventoryItemEndpoint : Endpoint<InventoryItemRequest, InventoryItemDto>
{
    private readonly ApplicationDbContext _db;

    public CreateInventoryItemEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/inventory/items");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(InventoryItemRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
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

        var entity = new InventoryItem
        {
            OrganizationId = access.Organization.Id,
            Name = values.Name,
            ItemType = values.ItemType,
            Unit = values.Unit,
            QuantityOnHand = values.QuantityOnHand,
            ReorderLevel = values.ReorderLevel,
            UnitCost = values.UnitCost,
            Supplier = values.Supplier,
            Notes = values.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.InventoryItems.Add(entity);
        await _db.SaveChangesAsync(ct);
        await SendAsync(InventoryMapping.ToDto(entity), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
