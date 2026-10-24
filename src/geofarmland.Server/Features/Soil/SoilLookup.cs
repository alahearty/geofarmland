using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Soil;

internal sealed record SoilLookupResult(Farm? Farm, LandParcel? LandParcel, string Error)
{
    public bool IsValid => Farm != null && LandParcel != null && Error.Length == 0;
}

internal static class SoilLookup
{
    public static async Task<SoilLookupResult> LoadAsync(
        ApplicationDbContext db,
        int organizationId,
        int? farmId,
        int? landParcelId,
        CancellationToken ct)
    {
        if (!farmId.HasValue || !landParcelId.HasValue)
            return new SoilLookupResult(null, null, "Select the farm and land parcel.");

        var farm = await db.Farms
            .FirstOrDefaultAsync(item => item.Id == farmId.Value && item.OrganizationId == organizationId, ct);
        if (farm == null)
            return new SoilLookupResult(null, null, "The selected farm was not found in this organization.");

        var parcel = await db.LandParcels
            .FirstOrDefaultAsync(item => item.Id == landParcelId.Value
                && item.OrganizationId == organizationId
                && item.FarmId == farm.Id, ct);
        if (parcel == null)
            return new SoilLookupResult(null, null, "The selected land parcel does not belong to that farm.");

        return new SoilLookupResult(farm, parcel, string.Empty);
    }
}
