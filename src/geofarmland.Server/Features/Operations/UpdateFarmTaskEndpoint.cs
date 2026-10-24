using FastEndpoints;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Operations;

public class UpdateFarmTaskEndpoint : Endpoint<FarmTaskRequest, FarmTaskDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateFarmTaskEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/operations/tasks/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(FarmTaskRequest req, CancellationToken ct)
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

        var lookup = await OperationLookup.LoadAsync(_db, task.OrganizationId, req, ct);
        if (!lookup.IsValid)
        {
            ThrowError(lookup.Error);
            return;
        }
        if (!FarmTaskValidation.TryNormalize(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        task.FarmId = values.FarmId;
        task.Farm = lookup.Farm!;
        task.LandParcelId = values.LandParcelId;
        task.LandParcel = lookup.LandParcel;
        task.Title = values.Title;
        task.TaskType = values.TaskType;
        task.Priority = values.Priority;
        task.Status = values.Status;
        task.DueDate = values.DueDate;
        task.Notes = values.Notes;
        task.CompletedAt = values.Status == FarmTaskStatus.Completed
            ? task.CompletedAt ?? DateTime.UtcNow
            : null;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await SendAsync(FarmTaskMapping.ToDto(task), cancellation: ct);
    }
}
