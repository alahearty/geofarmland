using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Features.Plots;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.LandParcels;

public class UpdateLandParcelEndpoint : Endpoint<LandParcelRequest, LandParcelDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateLandParcelEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/land-parcels/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(LandParcelRequest req, CancellationToken ct)
    {
        var parcelId = Route<int>("id");
        var parcel = await _db.LandParcels
            .Include(item => item.Farm)
            .FirstOrDefaultAsync(item => item.Id == parcelId, ct);
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
            ThrowError("Your organization role cannot update land parcels.", 403);
            return;
        }
        if (!LandParcelValidation.TryNormalize(req, true, parcel, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        var targetFarmId = req.FarmId ?? parcel.FarmId;
        var targetFarm = await _db.Farms.FirstOrDefaultAsync(farm =>
            farm.Id == targetFarmId && farm.OrganizationId == access.Organization.Id, ct);
        if (targetFarm == null)
        {
            ThrowError("The selected farm was not found in this organization.");
            return;
        }

        var geometry = parcel.Geometry;
        var geometryChanged = req.Coordinates != null;
        if (geometryChanged)
        {
            if (!PlotGeometry.TryCreate(req.Coordinates, out var polygon, out var geometryError))
            {
                ThrowError(geometryError);
                return;
            }
            geometry = polygon!;
        }

        if (await LandParcelGeometry.OverlapsExistingAsync(_db, targetFarm.Id, geometry, parcel.Id, ct))
        {
            ThrowError("The land parcel overlaps an existing parcel on this farm.");
            return;
        }
        if (values.Code != null && await _db.LandParcels.AnyAsync(item =>
                item.OrganizationId == access.Organization.Id
                && item.Id != parcel.Id
                && item.Code == values.Code, ct))
        {
            ThrowError("A land parcel with that code already exists in this organization.");
            return;
        }

        parcel.FarmId = targetFarm.Id;
        parcel.Farm = targetFarm;
        parcel.Name = values.Name;
        parcel.Code = values.Code;
        parcel.Geometry = geometry;
        parcel.AreaHectares = GeoMeasurement.AreaHectares(geometry);
        parcel.LandUse = values.LandUse;
        parcel.TenureType = values.TenureType;
        parcel.Status = values.Status;
        parcel.Notes = values.Notes;
        parcel.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await SendAsync(LandParcelMapping.ToDto(parcel), cancellation: ct);
    }
}
