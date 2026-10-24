using FastEndpoints;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Livestock;

public class GetLivestockOverviewEndpoint : EndpointWithoutRequest<LivestockOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetLivestockOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/livestock/overview");
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
        var groups = await _db.LivestockGroups
            .AsNoTracking()
            .Include(group => group.Farm)
            .Include(group => group.GrazingLandParcel)
            .Include(group => group.Events)
            .Where(group => group.OrganizationId == organizationId)
            .OrderBy(group => group.Name)
            .ToListAsync(ct);

        var liveGroups = groups
            .Where(group => group.Status is LivestockStatus.Active or LivestockStatus.Quarantine)
            .ToList();
        var totalHead = liveGroups.Sum(group => group.HeadCount);
        var weightedGroups = liveGroups.Where(group => group.AverageWeightKg.HasValue).ToList();
        var weightedHead = weightedGroups.Sum(group => group.HeadCount);
        var averageWeight = weightedHead > 0
            ? weightedGroups.Sum(group => (group.AverageWeightKg ?? 0m) * group.HeadCount) / weightedHead
            : (decimal?)null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(-30);
        var eventsLast30Days = await _db.LivestockEvents
            .AsNoTracking()
            .CountAsync(item => item.OrganizationId == organizationId && item.EventDate >= cutoff, ct);

        var recentEvents = await _db.LivestockEvents
            .AsNoTracking()
            .Include(item => item.LivestockGroup)
            .ThenInclude(group => group!.Farm)
            .Where(item => item.OrganizationId == organizationId)
            .OrderByDescending(item => item.EventDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(8)
            .ToListAsync(ct);

        var speciesBreakdown = liveGroups
            .GroupBy(group => group.Species)
            .Select(group => new SpeciesBreakdownDto(
                group.Key.ToString(),
                group.Count(),
                group.Sum(item => item.HeadCount)))
            .OrderByDescending(item => item.HeadCount)
            .ToList();

        await SendAsync(new LivestockOverviewResponse(
            access.CanManageParcels,
            new LivestockMetricsDto(
                totalHead,
                liveGroups.Count(group => group.Status == LivestockStatus.Active),
                liveGroups.Count(group => group.Status == LivestockStatus.Quarantine),
                eventsLast30Days,
                averageWeight),
            groups.Select(LivestockMapping.ToDto).ToList(),
            recentEvents.Select(LivestockMapping.ToDto).ToList(),
            speciesBreakdown), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
