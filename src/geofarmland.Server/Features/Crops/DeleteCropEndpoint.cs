using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Crops;

public class DeleteCropEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteCropEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/crops/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var cropId = Route<int>("id");
        var crop = await _db.Crops.FirstOrDefaultAsync(item => item.Id == cropId, ct);
        if (crop == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, crop.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage crops.", 403);
            return;
        }

        var hasSeasons = await _db.CropSeasons.AnyAsync(season => season.CropId == crop.Id, ct);
        if (hasSeasons)
        {
            ThrowError("Remove or reassign the crop seasons before deleting this crop.", 409);
            return;
        }

        _db.Crops.Remove(crop);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
