using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Livestock;

public class DeleteLivestockGroupEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteLivestockGroupEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/livestock/groups/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var groupId = Route<int>("id");
        var group = await _db.LivestockGroups.FirstOrDefaultAsync(item => item.Id == groupId, ct);
        if (group == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, group.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage livestock.", 403);
            return;
        }

        _db.LivestockGroups.Remove(group);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
