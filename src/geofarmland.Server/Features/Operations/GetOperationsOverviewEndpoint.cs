using FastEndpoints;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Operations;

public class GetOperationsOverviewEndpoint : EndpointWithoutRequest<OperationsOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetOperationsOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/operations/overview");
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

        var tasks = await _db.FarmTasks
            .AsNoTracking()
            .Include(task => task.Farm)
            .Include(task => task.LandParcel)
            .Where(task => task.OrganizationId == access.Organization.Id)
            .OrderBy(task => task.Status)
            .ThenBy(task => task.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(task => task.Priority)
            .Take(500)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekEnd = today.AddDays(7);
        var completedSince = DateTime.UtcNow.AddDays(-30);
        var openTasks = tasks
            .Where(task => task.Status is FarmTaskStatus.Pending or FarmTaskStatus.InProgress)
            .ToList();

        await SendAsync(new OperationsOverviewResponse(
            access.CanManageParcels,
            new OperationsMetricsDto(
                openTasks.Count,
                openTasks.Count(task => task.Status == FarmTaskStatus.InProgress),
                openTasks.Count(task => task.DueDate.HasValue && task.DueDate.Value < today),
                openTasks.Count(task => task.DueDate.HasValue
                    && task.DueDate.Value >= today
                    && task.DueDate.Value <= weekEnd),
                openTasks.Count(task => task.Priority is FarmTaskPriority.High or FarmTaskPriority.Urgent),
                tasks.Count(task => task.Status == FarmTaskStatus.Completed
                    && task.CompletedAt.HasValue
                    && task.CompletedAt.Value >= completedSince)),
            tasks.Select(FarmTaskMapping.ToDto).ToList()), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
