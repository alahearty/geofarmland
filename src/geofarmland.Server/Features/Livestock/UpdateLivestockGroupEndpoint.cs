using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Livestock;

public class UpdateLivestockGroupEndpoint : Endpoint<LivestockGroupRequest, LivestockGroupDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateLivestockGroupEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/livestock/groups/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(LivestockGroupRequest req, CancellationToken ct)
    {
        var groupId = Route<int>("id");
        var group = await _db.LivestockGroups
            .Include(item => item.Farm)
            .Include(item => item.GrazingLandParcel)
            .Include(item => item.Events)
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
            ThrowError("Your organization role cannot manage livestock.", 403);
            return;
        }

        var lookup = await LivestockLookup.LoadAsync(_db, group.OrganizationId, req, ct);
        if (!lookup.IsValid)
        {
            ThrowError(lookup.Error);
            return;
        }
        if (!LivestockValidation.TryNormalizeGroup(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        var duplicate = await _db.LivestockGroups.AnyAsync(item =>
            item.OrganizationId == group.OrganizationId
            && item.FarmId == values.FarmId
            && item.Id != group.Id
            && item.Name.ToLower() == values.Name.ToLower(), ct);
        if (duplicate)
        {
            ThrowError("A livestock group with that name already exists on the selected farm.");
            return;
        }

        group.FarmId = values.FarmId;
        group.Farm = lookup.Farm!;
        group.GrazingLandParcelId = values.GrazingLandParcelId;
        group.GrazingLandParcel = lookup.GrazingParcel;
        group.Name = values.Name;
        group.Species = values.Species;
        group.Breed = values.Breed;
        group.Purpose = values.Purpose;
        group.HeadCount = values.HeadCount;
        group.AverageWeightKg = values.AverageWeightKg;
        group.Status = values.Status;
        group.JoinedDate = values.JoinedDate;
        group.Notes = values.Notes;
        group.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await SendAsync(LivestockMapping.ToDto(group), cancellation: ct);
    }
}
