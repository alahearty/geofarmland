using FastEndpoints;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Crops;

public class GetCropsOverviewEndpoint : EndpointWithoutRequest<CropOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetCropsOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/crops/overview");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var organizationId = access.Organization.Id;
        var crops = await _db.Crops
            .AsNoTracking()
            .Include(crop => crop.Seasons)
            .Where(crop => crop.OrganizationId == organizationId)
            .OrderBy(crop => crop.Name)
            .ThenBy(crop => crop.Variety)
            .ToListAsync(ct);

        var seasons = await _db.CropSeasons
            .AsNoTracking()
            .Include(season => season.Crop)
            .Include(season => season.Farm)
            .Include(season => season.LandParcel)
            .Include(season => season.Harvests)
            .Where(season => season.OrganizationId == organizationId)
            .OrderByDescending(season => season.PlantingDate)
            .ThenBy(season => season.SeasonName)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeStatuses = new[] { CropSeasonStatus.Planted, CropSeasonStatus.Growing };
        var activeSeasons = seasons.Count(season => activeStatuses.Contains(season.Status));
        var plannedSeasons = seasons.Count(season => season.Status == CropSeasonStatus.Planned);
        var plantedHectares = seasons
            .Where(season => activeStatuses.Contains(season.Status))
            .Sum(season => season.AreaHectares);
        var expectedYield = seasons
            .Where(season => season.Status != CropSeasonStatus.Failed)
            .Sum(season => season.ExpectedYieldTons ?? 0m);
        var harvested = seasons.Sum(season => season.Harvests.Sum(harvest => harvest.QuantityTons));
        var upcomingHarvests = seasons.Count(season =>
            season.ExpectedHarvestDate.HasValue
            && season.ExpectedHarvestDate.Value >= today
            && season.Status is CropSeasonStatus.Planned or CropSeasonStatus.Planted or CropSeasonStatus.Growing);

        var recentHarvests = seasons
            .SelectMany(season => season.Harvests)
            .OrderByDescending(harvest => harvest.HarvestDate)
            .Take(8)
            .Select(CropMapping.ToDto)
            .ToList();

        await SendAsync(new CropOverviewResponse(
            access.CanManageParcels,
            new CropOverviewMetrics(
                crops.Count,
                activeSeasons,
                plannedSeasons,
                plantedHectares,
                expectedYield,
                harvested,
                upcomingHarvests),
            crops.Select(CropMapping.ToDto).ToList(),
            seasons.Select(CropMapping.ToDto).ToList(),
            recentHarvests), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
