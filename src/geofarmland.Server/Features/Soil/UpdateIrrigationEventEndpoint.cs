using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Soil;

public class UpdateIrrigationEventEndpoint : Endpoint<IrrigationEventRequest, IrrigationEventDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateIrrigationEventEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/soil/irrigation/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(IrrigationEventRequest req, CancellationToken ct)
    {
        var eventId = Route<int>("id");
        var irrigation = await _db.IrrigationEvents
            .Include(item => item.Farm)
            .Include(item => item.LandParcel)
            .FirstOrDefaultAsync(item => item.Id == eventId, ct);
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

        var lookup = await SoilLookup.LoadAsync(_db, irrigation.OrganizationId, req.FarmId, req.LandParcelId, ct);
        if (!lookup.IsValid)
        {
            ThrowError(lookup.Error);
            return;
        }
        if (!SoilValidation.TryNormalizeIrrigation(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        irrigation.FarmId = values.FarmId;
        irrigation.Farm = lookup.Farm!;
        irrigation.LandParcelId = values.LandParcelId;
        irrigation.LandParcel = lookup.LandParcel!;
        irrigation.EventDate = values.EventDate;
        irrigation.VolumeM3 = values.VolumeM3;
        irrigation.DurationMinutes = values.DurationMinutes;
        irrigation.Source = values.Source;
        irrigation.Notes = values.Notes;
        await _db.SaveChangesAsync(ct);

        await SendAsync(SoilMapping.ToDto(irrigation), cancellation: ct);
    }
}
