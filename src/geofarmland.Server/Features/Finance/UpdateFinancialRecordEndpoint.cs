using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Finance;

public class UpdateFinancialRecordEndpoint : Endpoint<FinancialRecordRequest, FinancialRecordDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateFinancialRecordEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/finance/records/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(FinancialRecordRequest req, CancellationToken ct)
    {
        var recordId = Route<int>("id");
        var record = await _db.FinancialRecords
            .Include(item => item.Farm)
            .FirstOrDefaultAsync(item => item.Id == recordId, ct);
        if (record == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, record.OrganizationId, ct);
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
                item.Id == values.FarmId.Value && item.OrganizationId == record.OrganizationId, ct);
            if (farm == null)
            {
                ThrowError("The selected farm was not found in this organization.");
                return;
            }
        }

        record.FarmId = values.FarmId;
        record.Farm = farm;
        record.RecordType = values.RecordType;
        record.Category = values.Category;
        record.Amount = values.Amount;
        record.RecordDate = values.RecordDate;
        record.Description = values.Description;
        await _db.SaveChangesAsync(ct);
        await SendAsync(FinanceMapping.ToDto(record), cancellation: ct);
    }
}
