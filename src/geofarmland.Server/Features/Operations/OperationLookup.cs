using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Operations;

internal sealed record OperationLookupResult(Farm? Farm, LandParcel? LandParcel, string Error)
{
    public bool IsValid => Farm != null && Error.Length == 0;
}

internal static class OperationLookup
{
    public static async Task<OperationLookupResult> LoadAsync(
        ApplicationDbContext db,
        int organizationId,
        FarmTaskRequest req,
        CancellationToken ct)
    {
        if (!req.FarmId.HasValue)
            return new OperationLookupResult(null, null, "Select the farm for this task.");

        var farm = await db.Farms
            .FirstOrDefaultAsync(item => item.Id == req.FarmId.Value && item.OrganizationId == organizationId, ct);
        if (farm == null)
            return new OperationLookupResult(null, null, "The selected farm was not found in this organization.");

        if (!req.LandParcelId.HasValue)
            return new OperationLookupResult(farm, null, string.Empty);

        var parcel = await db.LandParcels
            .FirstOrDefaultAsync(item => item.Id == req.LandParcelId.Value
                && item.OrganizationId == organizationId
                && item.FarmId == farm.Id, ct);
        if (parcel == null)
            return new OperationLookupResult(null, null, "The selected land parcel does not belong to that farm.");

        return new OperationLookupResult(farm, parcel, string.Empty);
    }
}
