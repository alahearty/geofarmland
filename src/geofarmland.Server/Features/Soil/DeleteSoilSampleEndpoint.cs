using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Soil;

public class DeleteSoilSampleEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteSoilSampleEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/soil/samples/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sampleId = Route<int>("id");
        var sample = await _db.SoilSamples.FirstOrDefaultAsync(item => item.Id == sampleId, ct);
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

        _db.SoilSamples.Remove(sample);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
