using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Finance;

public class CreateFinancialRecordEndpoint : Endpoint<FinancialRecordRequest, FinancialRecordDto>
{
    private readonly ApplicationDbContext _db;

    public CreateFinancialRecordEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/finance/records");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(FinancialRecordRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage finance records.", 403);
            return;
        }
        if (!FinanceValidation.TryNormalize(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        Farm? farm = null;
        if (values.FarmId.HasValue)
        {
            farm = await _db.Farms.FirstOrDefaultAsync(item =>
                item.Id == values.FarmId.Value && item.OrganizationId == access.Organization.Id, ct);
            if (farm == null)
            {
                ThrowError("The selected farm was not found in this organization.");
                return;
            }
        }

        var entity = new FinancialRecord
        {
            OrganizationId = access.Organization.Id,
            FarmId = values.FarmId,
            Farm = farm,
            RecordType = values.RecordType,
            Category = values.Category,
            Amount = values.Amount,
            RecordDate = values.RecordDate,
            Description = values.Description,
            CreatedAt = DateTime.UtcNow
        };
        _db.FinancialRecords.Add(entity);
        await _db.SaveChangesAsync(ct);
        await SendAsync(FinanceMapping.ToDto(entity), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
