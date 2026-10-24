using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Livestock;

public class CreateLivestockEventEndpoint : Endpoint<LivestockEventRequest, LivestockEventDto>
{
    private readonly ApplicationDbContext _db;

    public CreateLivestockEventEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/livestock/groups/{id}/events");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(LivestockEventRequest req, CancellationToken ct)
    {
        var groupId = Route<int>("id");
        var group = await _db.LivestockGroups
            .Include(item => item.Farm)
            .FirstOrDefaultAsync(item => item.Id == groupId, ct);
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
            ThrowError("Your organization role cannot record livestock events.", 403);
            return;
        }
        if (!LivestockValidation.TryNormalizeEvent(req, group, out var eventType, out var error))
        {
            ThrowError(error);
            return;
        }

        var entity = new LivestockEvent
        {
            OrganizationId = group.OrganizationId,
            LivestockGroupId = group.Id,
            LivestockGroup = group,
            EventType = eventType,
            EventDate = req.EventDate!.Value,
            HeadCount = req.HeadCount,
            WeightKg = req.WeightKg,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.LivestockEvents.Add(entity);

        if ((eventType is LivestockEventType.Sale or LivestockEventType.Mortality) && req.HeadCount.HasValue)
        {
            group.HeadCount = Math.Max(0, group.HeadCount - req.HeadCount.Value);
            if (group.HeadCount == 0)
                group.Status = eventType == LivestockEventType.Sale
                    ? LivestockStatus.Sold
                    : LivestockStatus.Archived;
        }
        if (eventType == LivestockEventType.Weight && req.WeightKg.HasValue)
        {
            group.AverageWeightKg = req.WeightKg.Value;
        }
        group.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await SendAsync(LivestockMapping.ToDto(entity), cancellation: ct);
    }
}
