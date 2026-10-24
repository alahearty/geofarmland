using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Livestock;

internal sealed record LivestockLookupResult(Farm? Farm, LandParcel? GrazingParcel, string Error)
{
    public bool IsValid => Farm != null && Error.Length == 0;
}

internal static class LivestockLookup
{
    public static async Task<LivestockLookupResult> LoadAsync(
        ApplicationDbContext db,
        int organizationId,
        LivestockGroupRequest req,
        CancellationToken ct)
    {
        if (!req.FarmId.HasValue)
            return new LivestockLookupResult(null, null, "Select the farm that owns this livestock group.");

        var farm = await db.Farms
            .FirstOrDefaultAsync(item => item.Id == req.FarmId.Value && item.OrganizationId == organizationId, ct);
        if (farm == null)
            return new LivestockLookupResult(null, null, "The selected farm was not found in this organization.");

        if (!req.GrazingLandParcelId.HasValue)
            return new LivestockLookupResult(farm, null, string.Empty);

        var parcel = await db.LandParcels
            .FirstOrDefaultAsync(item => item.Id == req.GrazingLandParcelId.Value
                && item.OrganizationId == organizationId
                && item.FarmId == farm.Id, ct);
        if (parcel == null)
            return new LivestockLookupResult(null, null, "The selected grazing parcel does not belong to that farm.");

        return new LivestockLookupResult(farm, parcel, string.Empty);
    }
}
