using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Crops;

public class CreateCropSeasonEndpoint : Endpoint<CropSeasonRequest, CropSeasonDto>
{
    private readonly ApplicationDbContext _db;

    public CreateCropSeasonEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/crop-seasons");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CropSeasonRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
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

        var lookup = await CropSeasonLookup.LoadAsync(_db, access.Organization.Id, req, ct);
        if (!lookup.IsValid)
        {
            ThrowError(lookup.Error);
            return;
        }
        if (!CropValidation.TryNormalizeSeason(req, lookup.LandParcel!, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        var duplicate = await _db.CropSeasons.AnyAsync(season =>
            season.OrganizationId == access.Organization.Id
            && season.LandParcelId == values.LandParcelId
            && season.SeasonName.ToLower() == values.SeasonName.ToLower(), ct);
        if (duplicate)
        {
            ThrowError("A season with that name already exists on the selected land parcel.");
            return;
        }

        var entity = new CropSeason
        {
            OrganizationId = access.Organization.Id,
            CropId = values.CropId,
            Crop = lookup.Crop!,
            FarmId = values.FarmId,
            Farm = lookup.Farm!,
            LandParcelId = values.LandParcelId,
            LandParcel = lookup.LandParcel!,
            SeasonName = values.SeasonName,
            Status = values.Status,
            PlantingDate = values.PlantingDate,
            ExpectedHarvestDate = values.ExpectedHarvestDate,
            AreaHectares = values.AreaHectares,
            ExpectedYieldTons = values.ExpectedYieldTons,
            Notes = values.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.CropSeasons.Add(entity);
        await _db.SaveChangesAsync(ct);

        await SendAsync(CropMapping.ToDto(entity), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
