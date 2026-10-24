using FastEndpoints;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Finance;

public class GetFinanceOverviewEndpoint : EndpointWithoutRequest<FinanceOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetFinanceOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/finance/overview");
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

        var records = await _db.FinancialRecords
            .AsNoTracking()
            .Include(record => record.Farm)
            .Where(record => record.OrganizationId == access.Organization.Id)
            .OrderByDescending(record => record.RecordDate)
            .ThenByDescending(record => record.CreatedAt)
            .Take(500)
            .ToListAsync(ct);
        var revenue = records
            .Where(record => record.RecordType == FinancialRecordType.Revenue)
            .Sum(record => record.Amount);
        var expenses = records
            .Where(record => record.RecordType == FinancialRecordType.Expense)
            .Sum(record => record.Amount);

        await SendAsync(new FinanceOverviewResponse(
            access.CanManageParcels,
            new FinanceMetricsDto(revenue, expenses, revenue - expenses, records.Count),
            records.Select(FinanceMapping.ToDto).ToList()), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
