using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Soil;

public class UpdateSoilSampleEndpoint : Endpoint<SoilSampleRequest, SoilSampleDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateSoilSampleEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/soil/samples/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(SoilSampleRequest req, CancellationToken ct)
    {
        var sampleId = Route<int>("id");
        var sample = await _db.SoilSamples
            .Include(item => item.Farm)
            .Include(item => item.LandParcel)
            .FirstOrDefaultAsync(item => item.Id == sampleId, ct);
        if (sample == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, sample.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage soil records.", 403);
            return;
        }

        var lookup = await SoilLookup.LoadAsync(_db, sample.OrganizationId, req.FarmId, req.LandParcelId, ct);
        if (!lookup.IsValid)
        {
            ThrowError(lookup.Error);
            return;
        }
        if (!SoilValidation.TryNormalizeSample(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        sample.FarmId = values.FarmId;
        sample.Farm = lookup.Farm!;
        sample.LandParcelId = values.LandParcelId;
        sample.LandParcel = lookup.LandParcel!;
        sample.SampleDate = values.SampleDate;
        sample.PH = values.PH;
        sample.OrganicMatterPct = values.OrganicMatterPct;
        sample.NitrogenPpm = values.NitrogenPpm;
        sample.PhosphorusPpm = values.PhosphorusPpm;
        sample.PotassiumPpm = values.PotassiumPpm;
        sample.Laboratory = values.Laboratory;
        sample.Notes = values.Notes;
        await _db.SaveChangesAsync(ct);

        await SendAsync(SoilMapping.ToDto(sample), cancellation: ct);
    }
}
