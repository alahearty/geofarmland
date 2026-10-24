using FastEndpoints;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.LandParcels;

public class GetLandParcelsEndpoint : EndpointWithoutRequest<LandParcelListResponse>
{
    private readonly ApplicationDbContext _db;

    public GetLandParcelsEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/land-parcels");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseInt("organizationId"), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var query = _db.LandParcels
            .AsNoTracking()
            .Include(parcel => parcel.Farm)
            .Where(parcel => parcel.OrganizationId == access.Organization.Id);

        var farmId = ParseInt("farmId");
        if (farmId.HasValue)
            query = query.Where(parcel => parcel.FarmId == farmId.Value);

        var search = HttpContext.Request.Query["search"].FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (search.Length > 100)
            {
                ThrowError("The search term cannot exceed 100 characters.");
                return;
            }
            var pattern = search.ToLower();
            query = query.Where(parcel =>
                parcel.Name.ToLower().Contains(pattern)
                || (parcel.Code != null && parcel.Code.ToLower().Contains(pattern)));
        }

        var status = HttpContext.Request.Query["status"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<LandParcelStatus>(status, true, out var parsedStatus))
            {
                ThrowError("Select a valid land parcel status.");
                return;
            }
            query = query.Where(parcel => parcel.Status == parsedStatus);
        }

        var landUse = HttpContext.Request.Query["landUse"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(landUse))
        {
            if (!Enum.TryParse<LandUseType>(landUse, true, out var parsedLandUse))
            {
                ThrowError("Select a valid land use.");
                return;
            }
            query = query.Where(parcel => parcel.LandUse == parsedLandUse);
        }

        var page = Math.Max(1, ParseInt("page") ?? 1);
        var pageSize = Math.Clamp(ParseInt("pageSize") ?? 100, 1, 250);
        var total = await query.CountAsync(ct);
        var parcels = await query
            .OrderBy(parcel => parcel.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        await SendAsync(new LandParcelListResponse(
            parcels.Select(LandParcelMapping.ToDto).ToList(),
            total,
            page,
            pageSize), cancellation: ct);
    }

    private int? ParseInt(string key)
    {
        var raw = HttpContext.Request.Query[key].FirstOrDefault();
        return int.TryParse(raw, out var value) ? value : null;
    }
}
