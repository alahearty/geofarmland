using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Devices;

public class CreateDeviceEndpoint : Endpoint<DeviceRequest, DeviceResponse>
{
    private readonly ApplicationDbContext _db;

    public CreateDeviceEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/devices");
        Policies("Authenticated");
        Throttle(30, 60);
    }

    public override async Task HandleAsync(DeviceRequest req, CancellationToken ct)
    {
        var ownerId = CurrentUser.Id(User);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var deviceId = req.DeviceId?.Trim() ?? string.Empty;
        var name = req.Name?.Trim() ?? string.Empty;
        if (deviceId.Length is < 1 or > 80 || name.Length is < 2 or > 120)
        {
            ThrowError("Device ID must be 1-80 characters and name 2-120 characters.");
            return;
        }

        if (req.PlotId.HasValue && !await CanUsePlotAsync(req.PlotId.Value, ownerId, ct))
        {
            ThrowError("The selected plot does not belong to you.");
            return;
        }
        if (req.LandParcelId.HasValue && !await CanUseLandParcelAsync(req.LandParcelId.Value, ownerId, ct))
        {
            ThrowError("The selected land parcel does not belong to your organization.");
            return;
        }

        if (await _db.Devices.AnyAsync(d => d.OwnerId == ownerId && d.DeviceId == deviceId, ct))
        {
            ThrowError("A device with that ID is already registered.");
            return;
        }

        var device = new Device
        {
            DeviceId = deviceId,
            Name = name,
            PlotId = req.PlotId,
            LandParcelId = req.LandParcelId,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(device).Reference(d => d.Plot).LoadAsync(ct);
        await _db.Entry(device).Reference(d => d.LandParcel).LoadAsync(ct);
        if (device.LandParcel != null)
            await _db.Entry(device.LandParcel).Reference(parcel => parcel.Farm).LoadAsync(ct);

        await SendAsync(GetDevicesEndpoint.ToResponse(device), cancellation: ct);
    }

    private async Task<bool> CanUsePlotAsync(int plotId, string ownerId, CancellationToken ct)
    {
        if (CurrentUser.IsAdmin(User)) return await _db.Plots.AnyAsync(p => p.Id == plotId, ct);
        return await _db.Plots.AnyAsync(p => p.Id == plotId && p.OwnerId == ownerId, ct);
    }

    private async Task<bool> CanUseLandParcelAsync(int landParcelId, string ownerId, CancellationToken ct)
    {
        var organizationId = await _db.LandParcels
            .Where(parcel => parcel.Id == landParcelId)
            .Select(parcel => (int?)parcel.OrganizationId)
            .FirstOrDefaultAsync(ct);
        if (!organizationId.HasValue)
            return false;
        if (CurrentUser.IsAdmin(User))
            return true;
        return await _db.OrganizationMembers.AnyAsync(member =>
            member.OrganizationId == organizationId.Value
            && member.UserId == ownerId
            && member.IsActive, ct);
    }
}
