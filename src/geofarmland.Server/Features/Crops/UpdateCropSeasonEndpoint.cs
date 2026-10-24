using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Crops;

public class UpdateCropSeasonEndpoint : Endpoint<CropSeasonRequest, CropSeasonDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateCropSeasonEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/crop-seasons/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CropSeasonRequest req, CancellationToken ct)
    {
        var seasonId = Route<int>("id");
        var season = await _db.CropSeasons
            .Include(item => item.Crop)
            .Include(item => item.Farm)
            .Include(item => item.LandParcel)
            .Include(item => item.Harvests)
            .FirstOrDefaultAsync(item => item.Id == seasonId, ct);
        if (season == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, season.OrganizationId, ct);
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

        var lookup = await CropSeasonLookup.LoadAsync(_db, season.OrganizationId, req, ct);
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

        var duplicate = await _db.CropSeasons.AnyAsync(item =>
            item.OrganizationId == season.OrganizationId
            && item.Id != season.Id
            && item.LandParcelId == values.LandParcelId
            && item.SeasonName.ToLower() == values.SeasonName.ToLower(), ct);
        if (duplicate)
        {
            ThrowError("A season with that name already exists on the selected land parcel.");
            return;
        }

        season.CropId = values.CropId;
        season.Crop = lookup.Crop!;
        season.FarmId = values.FarmId;
        season.Farm = lookup.Farm!;
        season.LandParcelId = values.LandParcelId;
        season.LandParcel = lookup.LandParcel!;
        season.SeasonName = values.SeasonName;
        season.Status = values.Status;
        season.PlantingDate = values.PlantingDate;
        season.ExpectedHarvestDate = values.ExpectedHarvestDate;
        season.AreaHectares = values.AreaHectares;
        season.ExpectedYieldTons = values.ExpectedYieldTons;
        season.Notes = values.Notes;
        season.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await SendAsync(CropMapping.ToDto(season), cancellation: ct);
    }
}
