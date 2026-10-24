using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Farms;

public class DeleteFarmEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteFarmEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/farms/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var farmId = Route<int>("id");
        var farm = await _db.Farms.FirstOrDefaultAsync(item => item.Id == farmId, ct);
        if (farm == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, farm.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageFarms)
        {
            ThrowError("Your organization role cannot delete farms.", 403);
            return;
        }

        var parcelCount = await _db.LandParcels.CountAsync(parcel => parcel.FarmId == farm.Id, ct);
        if (parcelCount > 0)
        {
            ThrowError("Move or archive the farm's land parcels before deleting the farm.", 409);
            return;
        }

        _db.Farms.Remove(farm);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
