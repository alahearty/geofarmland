using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Inventory;

public class DeleteInventoryItemEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteInventoryItemEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/inventory/items/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
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

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
