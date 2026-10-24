using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Features.Plots;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.LandParcels;

public class CreateLandParcelEndpoint : Endpoint<LandParcelRequest, LandParcelDto>
{
    private readonly ApplicationDbContext _db;

    public CreateLandParcelEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/land-parcels");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(LandParcelRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot create land parcels.", 403);
            return;
        }
        if (!req.FarmId.HasValue)
        {
            ThrowError("Select a farm for the land parcel.");
            return;
        }

        var farm = await _db.Farms.FirstOrDefaultAsync(item =>
            item.Id == req.FarmId.Value && item.OrganizationId == access.Organization.Id, ct);
        if (farm == null)
        {
            ThrowError("The selected farm was not found in this organization.");
            return;
        }
        if (!LandParcelValidation.TryNormalize(req, false, null, out var values, out var error))
        {
            ThrowError(error);
            return;
        }
        if (!PlotGeometry.TryCreate(req.Coordinates, out var polygon, out var geometryError))
        {
            ThrowError(geometryError);
            return;
        }
        if (await LandParcelGeometry.OverlapsExistingAsync(_db, farm.Id, polygon!, null, ct))
        {
            ThrowError("The land parcel overlaps an existing parcel on this farm.");
            return;
        }
        if (values.Code != null && await _db.LandParcels.AnyAsync(parcel =>
                parcel.OrganizationId == access.Organization.Id && parcel.Code == values.Code, ct))
        {
            ThrowError("A land parcel with that code already exists in this organization.");
            return;
        }

        var parcel = new LandParcel
        {
            OrganizationId = access.Organization.Id,
            FarmId = farm.Id,
            Farm = farm,
            Name = values.Name,
            Code = values.Code,
            Geometry = polygon!,
            AreaHectares = GeoMeasurement.AreaHectares(polygon!),
            LandUse = values.LandUse,
            TenureType = values.TenureType,
            Status = values.Status,
            Notes = values.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.LandParcels.Add(parcel);
        await _db.SaveChangesAsync(ct);

        await SendAsync(LandParcelMapping.ToDto(parcel), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
