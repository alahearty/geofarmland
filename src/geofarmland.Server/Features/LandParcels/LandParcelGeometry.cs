using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Geofarmland.Server.Features.LandParcels;

internal static class LandParcelGeometry
{
    public static async Task<bool> OverlapsExistingAsync(
        ApplicationDbContext db,
        int farmId,
        Polygon candidate,
        int? excludeParcelId,
        CancellationToken ct)
    {
        var existing = await db.LandParcels
            .AsNoTracking()
            .Where(parcel => parcel.FarmId == farmId
                && (!excludeParcelId.HasValue || parcel.Id != excludeParcelId.Value)
                && parcel.Geometry.Intersects(candidate))
            .Select(parcel => parcel.Geometry)
            .ToListAsync(ct);

        return existing.Any(geometry => geometry.Intersection(candidate).Area > 1e-12);
    }
}
