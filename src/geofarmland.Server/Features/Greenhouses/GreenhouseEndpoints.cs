using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Greenhouses;

public class GreenhouseUnitRequest
{
    public int? FarmId { get; set; }
    public int? LandParcelId { get; set; }
    public string? Name { get; set; }
    public string? StructureType { get; set; }
    public decimal? AreaM2 { get; set; }
    public string? CurrentCrop { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public sealed record GreenhouseUnitDto(
    int Id,
    int FarmId,
    string FarmName,
    int? LandParcelId,
    string? LandParcelName,
    string Name,
    string StructureType,
    decimal AreaM2,
    string? CurrentCrop,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record GreenhouseMetricsDto(
    int Units,
    int ActiveUnits,
    decimal CoveredAreaM2,
    int CroppedUnits);

public sealed record GreenhouseOverviewResponse(
    bool CanManage,
    GreenhouseMetricsDto Metrics,
    IReadOnlyList<GreenhouseUnitDto> Units);

public static class GreenhouseMapping
{
    public static GreenhouseUnitDto ToDto(GreenhouseUnit unit) => new(
        unit.Id,
        unit.FarmId,
        unit.Farm?.Name ?? "Unknown farm",
        unit.LandParcelId,
        unit.LandParcel?.Name,
        unit.Name,
        unit.StructureType.ToString(),
        unit.AreaM2,
        unit.CurrentCrop,
        unit.Status.ToString(),
        unit.Notes,
        unit.CreatedAt,
        unit.UpdatedAt);
}

internal readonly record struct GreenhouseValues(
    int FarmId,
    int? LandParcelId,
    string Name,
    GreenhouseStructureType StructureType,
    decimal AreaM2,
    string? CurrentCrop,
    GreenhouseStatus Status,
    string? Notes);

internal static class GreenhouseValidation
{
    public static bool TryNormalize(GreenhouseUnitRequest req, out GreenhouseValues values, out string error)
    {
        values = default;
        error = string.Empty;

        if (!req.FarmId.HasValue)
        {
            error = "Select the farm for this greenhouse unit.";
            return false;
        }

        var name = req.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120)
        {
            error = "A greenhouse unit name between 2 and 120 characters is required.";
            return false;
        }

        var structureType = GreenhouseStructureType.Greenhouse;
        if (!string.IsNullOrWhiteSpace(req.StructureType)
            && !Enum.TryParse(req.StructureType, true, out structureType))
        {
            error = "Select a valid structure type.";
            return false;
        }

        var status = GreenhouseStatus.Active;
        if (!string.IsNullOrWhiteSpace(req.Status) && !Enum.TryParse(req.Status, true, out status))
        {
            error = "Select a valid greenhouse status.";
            return false;
        }

        if (!req.AreaM2.HasValue || req.AreaM2.Value <= 0m)
        {
            error = "Covered area must be greater than zero.";
            return false;
        }

        var currentCrop = string.IsNullOrWhiteSpace(req.CurrentCrop) ? null : req.CurrentCrop.Trim();
        if (currentCrop is { Length: > 120 })
        {
            error = "Current crop cannot exceed 120 characters.";
            return false;
        }

        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        if (notes is { Length: > 1000 })
        {
            error = "Greenhouse notes cannot exceed 1000 characters.";
            return false;
        }

        values = new GreenhouseValues(
            req.FarmId.Value,
            req.LandParcelId,
            name,
            structureType,
            req.AreaM2.Value,
            currentCrop,
            status,
            notes);
        return true;
    }
}

internal sealed record GreenhouseLookupResult(Farm? Farm, LandParcel? LandParcel, string Error)
{
    public bool IsValid => Farm != null && Error.Length == 0;
}

internal static class GreenhouseLookup
{
    public static async Task<GreenhouseLookupResult> LoadAsync(
        ApplicationDbContext db,
        int organizationId,
        GreenhouseUnitRequest req,
        CancellationToken ct)
    {
        if (!req.FarmId.HasValue)
            return new GreenhouseLookupResult(null, null, "Select the farm for this greenhouse unit.");

        var farm = await db.Farms
            .FirstOrDefaultAsync(item => item.Id == req.FarmId.Value && item.OrganizationId == organizationId, ct);
        if (farm == null)
            return new GreenhouseLookupResult(null, null, "The selected farm was not found in this organization.");

        if (!req.LandParcelId.HasValue)
            return new GreenhouseLookupResult(farm, null, string.Empty);

        var parcel = await db.LandParcels
            .FirstOrDefaultAsync(item => item.Id == req.LandParcelId.Value
                && item.OrganizationId == organizationId
                && item.FarmId == farm.Id, ct);
        if (parcel == null)
            return new GreenhouseLookupResult(null, null, "The selected land parcel does not belong to that farm.");

        return new GreenhouseLookupResult(farm, parcel, string.Empty);
    }
}

public class GetGreenhouseOverviewEndpoint : EndpointWithoutRequest<GreenhouseOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetGreenhouseOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/greenhouses/overview");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var units = await _db.GreenhouseUnits
            .AsNoTracking()
            .Include(unit => unit.Farm)
            .Include(unit => unit.LandParcel)
            .Where(unit => unit.OrganizationId == access.Organization.Id)
            .OrderBy(unit => unit.Name)
            .ToListAsync(ct);

        await SendAsync(new GreenhouseOverviewResponse(
            access.CanManageParcels,
            new GreenhouseMetricsDto(
                units.Count,
                units.Count(unit => unit.Status == GreenhouseStatus.Active),
                units.Sum(unit => unit.AreaM2),
                units.Count(unit => !string.IsNullOrWhiteSpace(unit.CurrentCrop))),
            units.Select(GreenhouseMapping.ToDto).ToList()), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}

public class CreateGreenhouseUnitEndpoint : Endpoint<GreenhouseUnitRequest, GreenhouseUnitDto>
{
    private readonly ApplicationDbContext _db;

    public CreateGreenhouseUnitEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/greenhouses/units");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(GreenhouseUnitRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage greenhouse units.", 403);
            return;
        }

        var lookup = await GreenhouseLookup.LoadAsync(_db, access.Organization.Id, req, ct);
        if (!lookup.IsValid)
        {
            ThrowError(lookup.Error);
            return;
        }
        if (!GreenhouseValidation.TryNormalize(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        var entity = new GreenhouseUnit
        {
            OrganizationId = access.Organization.Id,
            FarmId = values.FarmId,
            Farm = lookup.Farm!,
            LandParcelId = values.LandParcelId,
            LandParcel = lookup.LandParcel,
            Name = values.Name,
            StructureType = values.StructureType,
            AreaM2 = values.AreaM2,
            CurrentCrop = values.CurrentCrop,
            Status = values.Status,
            Notes = values.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.GreenhouseUnits.Add(entity);
        await _db.SaveChangesAsync(ct);
        await SendAsync(GreenhouseMapping.ToDto(entity), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}

public class UpdateGreenhouseUnitEndpoint : Endpoint<GreenhouseUnitRequest, GreenhouseUnitDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateGreenhouseUnitEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/greenhouses/units/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(GreenhouseUnitRequest req, CancellationToken ct)
    {
        var unitId = Route<int>("id");
        var unit = await _db.GreenhouseUnits
            .Include(item => item.Farm)
            .Include(item => item.LandParcel)
            .FirstOrDefaultAsync(item => item.Id == unitId, ct);
        if (unit == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, unit.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage greenhouse units.", 403);
            return;
        }

        var lookup = await GreenhouseLookup.LoadAsync(_db, unit.OrganizationId, req, ct);
        if (!lookup.IsValid)
        {
            ThrowError(lookup.Error);
            return;
        }
        if (!GreenhouseValidation.TryNormalize(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        unit.FarmId = values.FarmId;
        unit.Farm = lookup.Farm!;
        unit.LandParcelId = values.LandParcelId;
        unit.LandParcel = lookup.LandParcel;
        unit.Name = values.Name;
        unit.StructureType = values.StructureType;
        unit.AreaM2 = values.AreaM2;
        unit.CurrentCrop = values.CurrentCrop;
        unit.Status = values.Status;
        unit.Notes = values.Notes;
        unit.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await SendAsync(GreenhouseMapping.ToDto(unit), cancellation: ct);
    }
}

public class DeleteGreenhouseUnitEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteGreenhouseUnitEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/greenhouses/units/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var unitId = Route<int>("id");
        var unit = await _db.GreenhouseUnits.FirstOrDefaultAsync(item => item.Id == unitId, ct);
        if (unit == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, unit.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage greenhouse units.", 403);
            return;
        }

        _db.GreenhouseUnits.Remove(unit);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
