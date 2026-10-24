using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Soil;

public class DeleteIrrigationEventEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteIrrigationEventEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/soil/irrigation/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var eventId = Route<int>("id");
        var irrigation = await _db.IrrigationEvents.FirstOrDefaultAsync(item => item.Id == eventId, ct);
        if (irrigation == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, irrigation.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage irrigation records.", 403);
            return;
        }

        _db.IrrigationEvents.Remove(irrigation);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
