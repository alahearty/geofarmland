using FastEndpoints;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Operations;

public class CompleteFarmTaskEndpoint : EndpointWithoutRequest<FarmTaskDto>
{
    private readonly ApplicationDbContext _db;

    public CompleteFarmTaskEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/operations/tasks/{id}/complete");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var taskId = Route<int>("id");
        var task = await _db.FarmTasks
            .Include(item => item.Farm)
            .Include(item => item.LandParcel)
            .FirstOrDefaultAsync(item => item.Id == taskId, ct);
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

        if (task.Status != FarmTaskStatus.Completed)
        {
            task.Status = FarmTaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        await SendAsync(FarmTaskMapping.ToDto(task), cancellation: ct);
    }
}
