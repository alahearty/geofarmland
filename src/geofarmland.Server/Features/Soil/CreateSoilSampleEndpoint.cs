using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;

namespace Geofarmland.Server.Features.Soil;

public class CreateSoilSampleEndpoint : Endpoint<SoilSampleRequest, SoilSampleDto>
{
    private readonly ApplicationDbContext _db;

    public CreateSoilSampleEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/soil/samples");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(SoilSampleRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
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

        var lookup = await SoilLookup.LoadAsync(_db, access.Organization.Id, req.FarmId, req.LandParcelId, ct);
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

        var entity = new SoilSample
        {
            OrganizationId = access.Organization.Id,
            FarmId = values.FarmId,
            Farm = lookup.Farm!,
            LandParcelId = values.LandParcelId,
            LandParcel = lookup.LandParcel!,
            SampleDate = values.SampleDate,
            PH = values.PH,
            OrganicMatterPct = values.OrganicMatterPct,
            NitrogenPpm = values.NitrogenPpm,
            PhosphorusPpm = values.PhosphorusPpm,
            PotassiumPpm = values.PotassiumPpm,
            Laboratory = values.Laboratory,
            Notes = values.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _db.SoilSamples.Add(entity);
        await _db.SaveChangesAsync(ct);

        await SendAsync(SoilMapping.ToDto(entity), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
