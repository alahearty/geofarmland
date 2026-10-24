using FastEndpoints;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Devices;

public class UpdateDeviceEndpoint : Endpoint<DeviceRequest, DeviceResponse>
{
    private readonly ApplicationDbContext _db;

    public UpdateDeviceEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/devices/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(DeviceRequest req, CancellationToken ct)
    {
        var id = Route<int>("id");
        var device = await _db.Devices.Include(d => d.Plot).SingleOrDefaultAsync(d => d.Id == id, ct);
        if (device == null || !CurrentUser.CanAccess(device.OwnerId, User))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var deviceId = req.DeviceId?.Trim() ?? string.Empty;
        var name = req.Name?.Trim() ?? string.Empty;
        if (deviceId.Length is < 1 or > 80 || name.Length is < 2 or > 120)
        {
            ThrowError("Device ID must be 1-80 characters and name 2-120 characters.");
            return;
        }

        if (req.PlotId.HasValue)
        {
            var plotAllowed = CurrentUser.IsAdmin(User)
                ? await _db.Plots.AnyAsync(p => p.Id == req.PlotId.Value, ct)
                : await _db.Plots.AnyAsync(p => p.Id == req.PlotId.Value && p.OwnerId == device.OwnerId, ct);
            if (!plotAllowed)
            {
                ThrowError("The selected plot does not belong to this device owner.");
                return;
            }
        }

        if (await _db.Devices.AnyAsync(d => d.Id != id && d.OwnerId == device.OwnerId && d.DeviceId == deviceId, ct))
        {
            ThrowError("A device with that ID is already registered.");
            return;
        }

        device.DeviceId = deviceId;
        device.Name = name;
        device.PlotId = req.PlotId;
        await _db.SaveChangesAsync(ct);

        var plot = req.PlotId.HasValue
            ? await _db.Plots.AsNoTracking().SingleOrDefaultAsync(p => p.Id == req.PlotId.Value, ct)
            : null;

        await SendAsync(new DeviceResponse(device.Id, device.DeviceId, device.Name, device.PlotId, plot?.Name, device.CreatedAt), cancellation: ct);
    }
}
