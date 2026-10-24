using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Farms;

public class UpdateFarmEndpoint : Endpoint<FarmRequest, FarmDetailDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateFarmEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/farms/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(FarmRequest req, CancellationToken ct)
    {
        var farmId = Route<int>("id");
        var farm = await _db.Farms
            .Include(item => item.LandParcels)
            .FirstOrDefaultAsync(item => item.Id == farmId, ct);
        if (farm == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, farm.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageFarms)
        {
            ThrowError("Your organization role cannot update farms.", 403);
            return;
        }
        if (!CreateFarmEndpoint.TryNormalize(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        var duplicateCode = values.Code != null && await _db.Farms.AnyAsync(
            item => item.OrganizationId == farm.OrganizationId && item.Id != farm.Id && item.Code == values.Code, ct);
        if (duplicateCode)
        {
            ThrowError("A farm with that code already exists in this organization.");
            return;
        }

        farm.Name = values.Name;
        farm.Code = values.Code;
        farm.Region = values.Region;
        farm.Country = values.Country;
        farm.Description = values.Description;
        farm.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await SendAsync(FarmMapping.ToDto(farm), cancellation: ct);
    }
}
