using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Livestock;

public class CreateLivestockGroupEndpoint : Endpoint<LivestockGroupRequest, LivestockGroupDto>
{
    private readonly ApplicationDbContext _db;

    public CreateLivestockGroupEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/livestock/groups");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(LivestockGroupRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
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

        var lookup = await LivestockLookup.LoadAsync(_db, access.Organization.Id, req, ct);
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

        var duplicate = await _db.LivestockGroups.AnyAsync(group =>
            group.OrganizationId == access.Organization.Id
            && group.FarmId == values.FarmId
            && group.Name.ToLower() == values.Name.ToLower(), ct);
        if (duplicate)
        {
            ThrowError("A livestock group with that name already exists on the selected farm.");
            return;
        }

        var entity = new LivestockGroup
        {
            OrganizationId = access.Organization.Id,
            FarmId = values.FarmId,
            Farm = lookup.Farm!,
            GrazingLandParcelId = values.GrazingLandParcelId,
            GrazingLandParcel = lookup.GrazingParcel,
            Name = values.Name,
            Species = values.Species,
            Breed = values.Breed,
            Purpose = values.Purpose,
            HeadCount = values.HeadCount,
            AverageWeightKg = values.AverageWeightKg,
            Status = values.Status,
            JoinedDate = values.JoinedDate,
            Notes = values.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.LivestockGroups.Add(entity);
        await _db.SaveChangesAsync(ct);

        await SendAsync(LivestockMapping.ToDto(entity), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
