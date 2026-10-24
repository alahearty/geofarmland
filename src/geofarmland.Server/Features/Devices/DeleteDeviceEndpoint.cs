using FastEndpoints;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Devices;

public class DeleteDeviceEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteDeviceEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/devices/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<int>("id");
        var device = await _db.Devices.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (device == null || !CurrentUser.CanAccess(device.OwnerId, User))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        _db.Devices.Remove(device);
        await _db.SaveChangesAsync(ct);
        await SendOkAsync(new { Message = "Device deleted." }, ct);
    }
}
