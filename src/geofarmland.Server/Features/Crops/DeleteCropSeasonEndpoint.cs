using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Crops;

public class DeleteCropSeasonEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteCropSeasonEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/crop-seasons/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var seasonId = Route<int>("id");
        var season = await _db.CropSeasons.FirstOrDefaultAsync(item => item.Id == seasonId, ct);
        if (season == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, season.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage crop seasons.", 403);
            return;
        }

        _db.CropSeasons.Remove(season);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
