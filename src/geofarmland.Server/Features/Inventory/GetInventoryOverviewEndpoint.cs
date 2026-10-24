using FastEndpoints;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Inventory;

public class GetInventoryOverviewEndpoint : EndpointWithoutRequest<InventoryOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetInventoryOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/inventory/overview");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var items = await _db.InventoryItems
            .AsNoTracking()
            .Where(item => item.OrganizationId == access.Organization.Id)
            .OrderBy(item => item.Name)
            .ToListAsync(ct);
        var metrics = new InventoryMetricsDto(
            items.Count,
            items.Count(item => item.QuantityOnHand <= item.ReorderLevel),
            items.Count(item => item.ItemType == InventoryItemType.Equipment),
            items.Sum(item => item.UnitCost.HasValue ? item.QuantityOnHand * item.UnitCost.Value : 0m));

        await SendAsync(new InventoryOverviewResponse(
            access.CanManageParcels,
            metrics,
            items.Select(InventoryMapping.ToDto).ToList()), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
