using FastEndpoints;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Analytics;

public sealed record AnalyticsMetricsDto(
    int LandParcels,
    decimal LandHectares,
    int ActiveCropSeasons,
    decimal CultivatedHectares,
    decimal ExpectedYieldTons,
    decimal HarvestedTons,
    decimal YieldAttainmentPercent,
    int LivestockHead,
    int ReportingDevices,
    int ReadingsLast24Hours,
    double RainfallLast24Hours);

public sealed record CropPerformanceDto(
    string CropName,
    int Seasons,
    decimal AreaHectares,
    decimal ExpectedYieldTons,
    decimal HarvestedTons,
    decimal AttainmentPercent);

public sealed record MonthlyHarvestDto(string Month, decimal QuantityTons);

public sealed record AnalyticsLandUseDto(string LandUse, int Parcels, decimal Hectares);

public sealed record AnalyticsSpeciesDto(string Species, int Groups, int HeadCount);

public sealed record YieldForecastDto(
    decimal ProjectedNextMonthTons,
    int MonthsWithHarvests,
    string Method);

public sealed record AnalyticsOverviewResponse(
    AnalyticsMetricsDto Metrics,
    IReadOnlyList<CropPerformanceDto> CropPerformance,
    IReadOnlyList<MonthlyHarvestDto> MonthlyHarvests,
    IReadOnlyList<AnalyticsLandUseDto> LandUse,
    IReadOnlyList<AnalyticsSpeciesDto> Species,
    YieldForecastDto? Forecast);

public class GetAnalyticsOverviewEndpoint : EndpointWithoutRequest<AnalyticsOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetAnalyticsOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/analytics/overview");
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
        var parcels = await _db.LandParcels
            .AsNoTracking()
            .Where(parcel => parcel.OrganizationId == organizationId)
            .Select(parcel => new { parcel.LandUse, parcel.AreaHectares })
            .ToListAsync(ct);

        var seasons = await _db.CropSeasons
            .AsNoTracking()
            .Include(season => season.Crop)
            .Include(season => season.Harvests)
            .Where(season => season.OrganizationId == organizationId)
            .ToListAsync(ct);

        var livestock = await _db.LivestockGroups
            .AsNoTracking()
            .Where(group => group.OrganizationId == organizationId
                && (group.Status == LivestockStatus.Active || group.Status == LivestockStatus.Quarantine))
            .Select(group => new { group.Species, group.HeadCount })
            .ToListAsync(ct);

        var memberIds = await OrganizationAccess.GetMemberUserIdsAsync(_db, organizationId, ct);
        var since = DateTime.UtcNow.AddHours(-24);
        var recentReadings = await _db.SensorData
            .AsNoTracking()
            .Where(reading => reading.OwnerId != null
                && memberIds.Contains(reading.OwnerId)
                && reading.Timestamp >= since)
            .Select(reading => new { reading.OwnerId, reading.DeviceId, reading.Rainfall, reading.Timestamp })
            .ToListAsync(ct);

        var activeStatuses = new[] { CropSeasonStatus.Planted, CropSeasonStatus.Growing };
        var activeSeasons = seasons.Where(season => activeStatuses.Contains(season.Status)).ToList();
        var cultivatedHectares = activeSeasons.Sum(season => season.AreaHectares);
        var expectedYield = seasons
            .Where(season => season.Status != CropSeasonStatus.Failed)
            .Sum(season => season.ExpectedYieldTons ?? 0m);
        var harvestedTons = seasons.Sum(season => season.Harvests.Sum(harvest => harvest.QuantityTons));
        var attainment = expectedYield > 0m
            ? Math.Round(harvestedTons / expectedYield * 100m, 1)
            : 0m;

        var cropPerformance = seasons
            .GroupBy(season => season.Crop?.Name ?? "Unknown crop")
            .Select(group =>
            {
                var expected = group.Sum(season => season.ExpectedYieldTons ?? 0m);
                var harvested = group.Sum(season => season.Harvests.Sum(harvest => harvest.QuantityTons));
                return new CropPerformanceDto(
                    group.Key,
                    group.Count(),
                    group.Sum(season => season.AreaHectares),
                    expected,
                    harvested,
                    expected > 0m ? Math.Round(harvested / expected * 100m, 1) : 0m);
            })
            .OrderByDescending(item => item.HarvestedTons)
            .ThenByDescending(item => item.AreaHectares)
            .ToList();

        var lastSixMonths = Enumerable.Range(0, 6)
            .Select(offset => new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(offset - 5))
            .ToList();
        var harvests = seasons.SelectMany(season => season.Harvests).ToList();
        var monthlyHarvests = lastSixMonths
            .Select(month => new MonthlyHarvestDto(
                month.ToString("yyyy-MM"),
                harvests
                    .Where(harvest => harvest.HarvestDate.Year == month.Year && harvest.HarvestDate.Month == month.Month)
                    .Sum(harvest => harvest.QuantityTons)))
            .ToList();

        var landUse = parcels
            .GroupBy(parcel => parcel.LandUse)
            .Select(group => new AnalyticsLandUseDto(
                group.Key.ToString(),
                group.Count(),
                group.Sum(parcel => parcel.AreaHectares)))
            .OrderByDescending(item => item.Hectares)
            .ToList();

        var species = livestock
            .GroupBy(group => group.Species)
            .Select(group => new AnalyticsSpeciesDto(
                group.Key.ToString(),
                group.Count(),
                group.Sum(item => item.HeadCount)))
            .OrderByDescending(item => item.HeadCount)
            .ToList();

        var monthsWithHarvests = monthlyHarvests.Count(item => item.QuantityTons > 0m);
        YieldForecastDto? forecast = null;
        if (monthsWithHarvests >= 3)
        {
            var values = monthlyHarvests.Select(item => (double)item.QuantityTons).ToList();
            var meanX = (values.Count - 1) / 2d;
            var meanY = values.Average();
            var numerator = 0d;
            var denominator = 0d;
            for (var index = 0; index < values.Count; index++)
            {
                numerator += (index - meanX) * (values[index] - meanY);
                denominator += Math.Pow(index - meanX, 2);
            }
            var slope = denominator == 0d ? 0d : numerator / denominator;
            var projected = Math.Max(0d, meanY + slope * (values.Count - meanX));
            forecast = new YieldForecastDto(
                Math.Round((decimal)projected, 3),
                monthsWithHarvests,
                "Linear trend across six months of recorded harvests");
        }

        await SendAsync(new AnalyticsOverviewResponse(
            new AnalyticsMetricsDto(
                parcels.Count,
                parcels.Sum(parcel => parcel.AreaHectares),
                activeSeasons.Count,
                cultivatedHectares,
                expectedYield,
                harvestedTons,
                attainment,
                livestock.Sum(group => group.HeadCount),
                recentReadings.Select(reading => $"{reading.OwnerId}|{reading.DeviceId}").Distinct().Count(),
                recentReadings.Count,
                Math.Round(recentReadings.Sum(reading => reading.Rainfall), 2)),
            cropPerformance,
            monthlyHarvests,
            landUse,
            species,
            forecast), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
