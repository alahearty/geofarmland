using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Operations;

public class DeleteFarmTaskEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteFarmTaskEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/operations/tasks/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var taskId = Route<int>("id");
        var task = await _db.FarmTasks.FirstOrDefaultAsync(item => item.Id == taskId, ct);
        if (task == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, task.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage field operations.", 403);
            return;
        }

        _db.FarmTasks.Remove(task);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
