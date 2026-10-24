using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;

namespace Geofarmland.Server.Features.Farms;

public class GetFarmsEndpoint : EndpointWithoutRequest<List<FarmDetailDto>>
{
    private readonly ApplicationDbContext _db;

    public GetFarmsEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/farms");
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

        await SendAsync(await FarmMapping.LoadFarmsAsync(_db, access.Organization.Id, ct), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
