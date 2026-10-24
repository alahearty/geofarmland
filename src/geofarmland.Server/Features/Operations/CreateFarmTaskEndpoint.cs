using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;

namespace Geofarmland.Server.Features.Operations;

public class CreateFarmTaskEndpoint : Endpoint<FarmTaskRequest, FarmTaskDto>
{
    private readonly ApplicationDbContext _db;

    public CreateFarmTaskEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/operations/tasks");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(FarmTaskRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
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

        var lookup = await OperationLookup.LoadAsync(_db, access.Organization.Id, req, ct);
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

        var entity = new FarmTask
        {
            OrganizationId = access.Organization.Id,
            FarmId = values.FarmId,
            Farm = lookup.Farm!,
            LandParcelId = values.LandParcelId,
            LandParcel = lookup.LandParcel,
            Title = values.Title,
            TaskType = values.TaskType,
            Priority = values.Priority,
            Status = values.Status,
            DueDate = values.DueDate,
            Notes = values.Notes,
            CompletedAt = values.Status == FarmTaskStatus.Completed ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.FarmTasks.Add(entity);
        await _db.SaveChangesAsync(ct);

        await SendAsync(FarmTaskMapping.ToDto(entity), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
