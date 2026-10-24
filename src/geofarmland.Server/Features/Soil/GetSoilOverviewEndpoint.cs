using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Soil;

public class GetSoilOverviewEndpoint : EndpointWithoutRequest<SoilOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetSoilOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/soil/overview");
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
        var sampleQuery = _db.SoilSamples
            .AsNoTracking()
            .Where(sample => sample.OrganizationId == organizationId);
        var sampleCount = await sampleQuery.CountAsync(ct);
        var averagePh = await sampleQuery.AverageAsync(sample => (decimal?)sample.PH, ct);
        var averageOrganicMatter = await sampleQuery.AverageAsync(sample => (decimal?)sample.OrganicMatterPct, ct);

        var since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var irrigationQuery = _db.IrrigationEvents
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId);
        var irrigationCount = await irrigationQuery.CountAsync(item => item.EventDate >= since, ct);
        var waterVolume = await irrigationQuery
            .Where(item => item.EventDate >= since)
            .SumAsync(item => (decimal?)item.VolumeM3, ct) ?? 0m;

        var samples = await sampleQuery
            .Include(sample => sample.Farm)
            .Include(sample => sample.LandParcel)
            .OrderByDescending(sample => sample.SampleDate)
            .ThenByDescending(sample => sample.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var irrigationEvents = await irrigationQuery
            .Include(item => item.Farm)
            .Include(item => item.LandParcel)
            .OrderByDescending(item => item.EventDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        await SendAsync(new SoilOverviewResponse(
            access.CanManageParcels,
            new SoilMetricsDto(
                sampleCount,
                averagePh.HasValue ? Math.Round(averagePh.Value, 2) : null,
                averageOrganicMatter.HasValue ? Math.Round(averageOrganicMatter.Value, 2) : null,
                irrigationCount,
                waterVolume),
            samples.Select(SoilMapping.ToDto).ToList(),
            irrigationEvents.Select(SoilMapping.ToDto).ToList()), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
