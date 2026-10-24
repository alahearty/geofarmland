using Geofarmland.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Farms;

public class FarmRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }
    public string? Description { get; set; }
}

public sealed record FarmDetailDto(
    int Id,
    int OrganizationId,
    string Name,
    string? Code,
    string? Region,
    string? Country,
    string? Description,
    int ParcelCount,
    decimal Hectares,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public static class FarmMapping
{
    public static FarmDetailDto ToDto(Farm farm) => new(
        farm.Id,
        farm.OrganizationId,
        farm.Name,
        farm.Code,
        farm.Region,
        farm.Country,
        farm.Description,
        farm.LandParcels.Count,
        farm.LandParcels.Sum(parcel => parcel.AreaHectares),
        farm.CreatedAt,
        farm.UpdatedAt);

    public static async Task<List<FarmDetailDto>> LoadFarmsAsync(
        DbContext db,
        int organizationId,
        CancellationToken ct)
    {
        return await db.Set<Farm>()
            .AsNoTracking()
            .Where(farm => farm.OrganizationId == organizationId)
            .OrderBy(farm => farm.Name)
            .Select(farm => new FarmDetailDto(
                farm.Id,
                farm.OrganizationId,
                farm.Name,
                farm.Code,
                farm.Region,
                farm.Country,
                farm.Description,
                farm.LandParcels.Count,
                farm.LandParcels.Sum(parcel => (decimal?)parcel.AreaHectares) ?? 0m,
                farm.CreatedAt,
                farm.UpdatedAt))
            .ToListAsync(ct);
    }
}
