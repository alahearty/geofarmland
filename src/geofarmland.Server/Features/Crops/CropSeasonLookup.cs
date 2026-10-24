using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Crops;

internal sealed record CropSeasonLookupResult(
    Crop? Crop,
    Farm? Farm,
    LandParcel? LandParcel,
    string Error)
{
    public bool IsValid => Crop != null && Farm != null && LandParcel != null && Error.Length == 0;
}

internal static class CropSeasonLookup
{
    public static async Task<CropSeasonLookupResult> LoadAsync(
        ApplicationDbContext db,
        int organizationId,
        CropSeasonRequest req,
        CancellationToken ct)
    {
        if (!req.CropId.HasValue || !req.FarmId.HasValue || !req.LandParcelId.HasValue)
            return new CropSeasonLookupResult(null, null, null, "Select a crop, farm and land parcel for this season.");

        var crop = await db.Crops
            .FirstOrDefaultAsync(item => item.Id == req.CropId.Value && item.OrganizationId == organizationId, ct);
        if (crop == null)
            return new CropSeasonLookupResult(null, null, null, "The selected crop was not found in this organization.");

        var farm = await db.Farms
            .FirstOrDefaultAsync(item => item.Id == req.FarmId.Value && item.OrganizationId == organizationId, ct);
        if (farm == null)
            return new CropSeasonLookupResult(null, null, null, "The selected farm was not found in this organization.");

        var parcel = await db.LandParcels
            .FirstOrDefaultAsync(item => item.Id == req.LandParcelId.Value
                && item.OrganizationId == organizationId
                && item.FarmId == farm.Id, ct);
        if (parcel == null)
            return new CropSeasonLookupResult(null, null, null, "The selected land parcel does not belong to that farm.");

        return new CropSeasonLookupResult(crop, farm, parcel, string.Empty);
    }
}
