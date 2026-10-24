using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Devices;

public class GetDevicesEndpoint : EndpointWithoutRequest<List<DeviceResponse>>
{
    private readonly ApplicationDbContext _db;

    public GetDevicesEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/devices");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var ownerId = CurrentUser.Id(User);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var query = _db.Devices.AsNoTracking().Include(d => d.Plot).AsQueryable();
        if (!CurrentUser.IsAdmin(User))
            query = query.Where(d => d.OwnerId == ownerId);

        var devices = await query.OrderBy(d => d.Name).ToListAsync(ct);
        await SendAsync(devices.Select(ToResponse).ToList(), cancellation: ct);
    }

    internal static DeviceResponse ToResponse(Device device) => new(
        device.Id,
        device.DeviceId,
        device.Name,
        device.PlotId,
        device.Plot?.Name,
        device.CreatedAt);
}

