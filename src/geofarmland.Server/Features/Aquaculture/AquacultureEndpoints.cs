using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Aquaculture;

public class AquaculturePondRequest
{
    public int? FarmId { get; set; }
    public int? LandParcelId { get; set; }
    public string? Name { get; set; }
    public string? WaterType { get; set; }
    public string? SystemType { get; set; }
    public decimal? AreaM2 { get; set; }
    public string? Species { get; set; }
    public int? StockCount { get; set; }
    public decimal? AverageWeightG { get; set; }
    public DateOnly? LastStockedDate { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public sealed record AquaculturePondDto(
    int Id,
    int FarmId,
    string FarmName,
    int? LandParcelId,
    string? LandParcelName,
    string Name,
    string WaterType,
    string SystemType,
    decimal AreaM2,
    string? Species,
    int StockCount,
    decimal? AverageWeightG,
    DateOnly? LastStockedDate,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record AquacultureMetricsDto(
    int Ponds,
    int ActivePonds,
    int StockedPonds,
    decimal WaterAreaM2,
    int TotalStock);

public sealed record AquacultureOverviewResponse(
    bool CanManage,
    AquacultureMetricsDto Metrics,
    IReadOnlyList<AquaculturePondDto> Ponds);

public static class AquacultureMapping
{
    public static AquaculturePondDto ToDto(AquaculturePond pond) => new(
        pond.Id,
        pond.FarmId,
        pond.Farm?.Name ?? "Unknown farm",
        pond.LandParcelId,
        pond.LandParcel?.Name,
        pond.Name,
        pond.WaterType.ToString(),
        pond.SystemType.ToString(),
        pond.AreaM2,
        pond.Species,
        pond.StockCount,
        pond.AverageWeightG,
        pond.LastStockedDate,
        pond.Status.ToString(),
        pond.Notes,
        pond.CreatedAt,
        pond.UpdatedAt);
}

internal readonly record struct AquacultureValues(
    int FarmId,
    int? LandParcelId,
    string Name,
    WaterType WaterType,
    AquacultureSystem SystemType,
    decimal AreaM2,
    string? Species,
    int StockCount,
    decimal? AverageWeightG,
    DateOnly? LastStockedDate,
    AquacultureStatus Status,
    string? Notes);

internal static class AquacultureValidation
{
    public static bool TryNormalize(AquaculturePondRequest req, out AquacultureValues values, out string error)
    {
        values = default;
        error = string.Empty;

        if (!req.FarmId.HasValue)
        {
            error = "Select the farm for this pond.";
            return false;
        }

        var name = req.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120)
        {
            error = "A pond name between 2 and 120 characters is required.";
            return false;
        }

        var waterType = WaterType.Freshwater;
        if (!string.IsNullOrWhiteSpace(req.WaterType) && !Enum.TryParse(req.WaterType, true, out waterType))
        {
            error = "Select a valid water type.";
            return false;
        }

        var systemType = AquacultureSystem.Pond;
        if (!string.IsNullOrWhiteSpace(req.SystemType) && !Enum.TryParse(req.SystemType, true, out systemType))
        {
            error = "Select a valid aquaculture system.";
            return false;
        }

        var status = AquacultureStatus.Active;
        if (!string.IsNullOrWhiteSpace(req.Status) && !Enum.TryParse(req.Status, true, out status))
        {
            error = "Select a valid pond status.";
            return false;
        }

        if (!req.AreaM2.HasValue || req.AreaM2.Value <= 0m)
        {
            error = "Pond area must be greater than zero.";
            return false;
        }

        var species = string.IsNullOrWhiteSpace(req.Species) ? null : req.Species.Trim();
        if (species is { Length: > 120 })
        {
            error = "Species cannot exceed 120 characters.";
            return false;
        }

        var stockCount = req.StockCount ?? 0;
        if (stockCount < 0)
        {
            error = "Stock count cannot be negative.";
            return false;
        }

        if (req.AverageWeightG is < 0m)
        {
            error = "Average weight cannot be negative.";
            return false;
        }

        if (req.LastStockedDate.HasValue && req.LastStockedDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            error = "The last stocked date cannot be in the future.";
            return false;
        }

        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        if (notes is { Length: > 1000 })
        {
            error = "Pond notes cannot exceed 1000 characters.";
            return false;
        }

        values = new AquacultureValues(
            req.FarmId.Value,
            req.LandParcelId,
            name,
            waterType,
            systemType,
            req.AreaM2.Value,
            species,
            stockCount,
            req.AverageWeightG,
            req.LastStockedDate,
            status,
            notes);
        return true;
    }
}

internal sealed record AquacultureLookupResult(Farm? Farm, LandParcel? LandParcel, string Error)
{
    public bool IsValid => Farm != null && Error.Length == 0;
}

internal static class AquacultureLookup
{
    public static async Task<AquacultureLookupResult> LoadAsync(
        ApplicationDbContext db,
        int organizationId,
        AquaculturePondRequest req,
        CancellationToken ct)
    {
        if (!req.FarmId.HasValue)
            return new AquacultureLookupResult(null, null, "Select the farm for this pond.");

        var farm = await db.Farms
            .FirstOrDefaultAsync(item => item.Id == req.FarmId.Value && item.OrganizationId == organizationId, ct);
        if (farm == null)
            return new AquacultureLookupResult(null, null, "The selected farm was not found in this organization.");

        if (!req.LandParcelId.HasValue)
            return new AquacultureLookupResult(farm, null, string.Empty);

        var parcel = await db.LandParcels
            .FirstOrDefaultAsync(item => item.Id == req.LandParcelId.Value
                && item.OrganizationId == organizationId
                && item.FarmId == farm.Id, ct);
        if (parcel == null)
            return new AquacultureLookupResult(null, null, "The selected land parcel does not belong to that farm.");

        return new AquacultureLookupResult(farm, parcel, string.Empty);
    }
}

public class GetAquacultureOverviewEndpoint : EndpointWithoutRequest<AquacultureOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetAquacultureOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/aquaculture/overview");
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

        var ponds = await _db.AquaculturePonds
            .AsNoTracking()
            .Include(pond => pond.Farm)
            .Include(pond => pond.LandParcel)
            .Where(pond => pond.OrganizationId == access.Organization.Id)
            .OrderBy(pond => pond.Name)
            .ToListAsync(ct);

        await SendAsync(new AquacultureOverviewResponse(
            access.CanManageParcels,
            new AquacultureMetricsDto(
                ponds.Count,
                ponds.Count(pond => pond.Status == AquacultureStatus.Active),
                ponds.Count(pond => pond.StockCount > 0),
                ponds.Sum(pond => pond.AreaM2),
                ponds.Sum(pond => pond.StockCount)),
            ponds.Select(AquacultureMapping.ToDto).ToList()), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}

public class CreateAquaculturePondEndpoint : Endpoint<AquaculturePondRequest, AquaculturePondDto>
{
    private readonly ApplicationDbContext _db;

    public CreateAquaculturePondEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/aquaculture/ponds");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(AquaculturePondRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage aquaculture.", 403);
            return;
        }

        var lookup = await AquacultureLookup.LoadAsync(_db, access.Organization.Id, req, ct);
        if (!lookup.IsValid)
        {
            ThrowError(lookup.Error);
            return;
        }
        if (!AquacultureValidation.TryNormalize(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        var entity = new AquaculturePond
        {
            OrganizationId = access.Organization.Id,
            FarmId = values.FarmId,
            Farm = lookup.Farm!,
            LandParcelId = values.LandParcelId,
            LandParcel = lookup.LandParcel,
            Name = values.Name,
            WaterType = values.WaterType,
            SystemType = values.SystemType,
            AreaM2 = values.AreaM2,
            Species = values.Species,
            StockCount = values.StockCount,
            AverageWeightG = values.AverageWeightG,
            LastStockedDate = values.LastStockedDate,
            Status = values.Status,
            Notes = values.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.AquaculturePonds.Add(entity);
        await _db.SaveChangesAsync(ct);
        await SendAsync(AquacultureMapping.ToDto(entity), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}

public class UpdateAquaculturePondEndpoint : Endpoint<AquaculturePondRequest, AquaculturePondDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateAquaculturePondEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/aquaculture/ponds/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(AquaculturePondRequest req, CancellationToken ct)
    {
        var pondId = Route<int>("id");
        var pond = await _db.AquaculturePonds
            .Include(item => item.Farm)
            .Include(item => item.LandParcel)
            .FirstOrDefaultAsync(item => item.Id == pondId, ct);
        if (pond == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, pond.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage aquaculture.", 403);
            return;
        }

        var lookup = await AquacultureLookup.LoadAsync(_db, pond.OrganizationId, req, ct);
        if (!lookup.IsValid)
        {
            ThrowError(lookup.Error);
            return;
        }
        if (!AquacultureValidation.TryNormalize(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        pond.FarmId = values.FarmId;
        pond.Farm = lookup.Farm!;
        pond.LandParcelId = values.LandParcelId;
        pond.LandParcel = lookup.LandParcel;
        pond.Name = values.Name;
        pond.WaterType = values.WaterType;
        pond.SystemType = values.SystemType;
        pond.AreaM2 = values.AreaM2;
        pond.Species = values.Species;
        pond.StockCount = values.StockCount;
        pond.AverageWeightG = values.AverageWeightG;
        pond.LastStockedDate = values.LastStockedDate;
        pond.Status = values.Status;
        pond.Notes = values.Notes;
        pond.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await SendAsync(AquacultureMapping.ToDto(pond), cancellation: ct);
    }
}

public class DeleteAquaculturePondEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public DeleteAquaculturePondEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/api/aquaculture/ponds/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pondId = Route<int>("id");
        var pond = await _db.AquaculturePonds.FirstOrDefaultAsync(item => item.Id == pondId, ct);
        if (pond == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, pond.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage aquaculture.", 403);
            return;
        }

        _db.AquaculturePonds.Remove(pond);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
