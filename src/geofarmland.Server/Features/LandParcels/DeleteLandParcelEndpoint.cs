using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.LandParcels;

public class DeleteLandParcelEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteLandParcelEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/land-parcels/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var parcelId = Route<int>("id");
        var parcel = await _db.LandParcels.FirstOrDefaultAsync(item => item.Id == parcelId, ct);
        if (parcel == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, parcel.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot delete land parcels.", 403);
            return;
        }

        _db.LandParcels.Remove(parcel);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
