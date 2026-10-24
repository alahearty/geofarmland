using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Finance;

public class DeleteFinancialRecordEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteFinancialRecordEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/finance/records/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var recordId = Route<int>("id");
        var record = await _db.FinancialRecords.FirstOrDefaultAsync(item => item.Id == recordId, ct);
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

        _db.FinancialRecords.Remove(record);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
