using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Farms;

public class CreateFarmEndpoint : Endpoint<FarmRequest, FarmDetailDto>
{
    private readonly ApplicationDbContext _db;

    public CreateFarmEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/farms");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(FarmRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageFarms)
        {
            ThrowError("Your organization role cannot create farms.", 403);
            return;
        }

        if (!TryNormalize(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        var duplicateCode = values.Code != null && await _db.Farms.AnyAsync(
            farm => farm.OrganizationId == access.Organization.Id && farm.Code == values.Code, ct);
        if (duplicateCode)
        {
            ThrowError("A farm with that code already exists in this organization.");
            return;
        }

        var farm = new Farm
        {
            OrganizationId = access.Organization.Id,
            Name = values.Name,
            Code = values.Code,
            Region = values.Region,
            Country = values.Country,
            Description = values.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Farms.Add(farm);
        await _db.SaveChangesAsync(ct);

        await SendAsync(FarmMapping.ToDto(farm), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }

    internal static bool TryNormalize(FarmRequest req, out FarmValues values, out string error)
    {
        values = default;
        error = string.Empty;

        var name = req.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 160)
        {
            error = "A farm name between 2 and 160 characters is required.";
            return false;
        }

        var code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim().ToUpperInvariant();
        if (code is { Length: > 40 })
        {
            error = "A farm code cannot exceed 40 characters.";
            return false;
        }

        var region = Clean(req.Region, 120, out var regionValid);
        var country = Clean(req.Country, 80, out var countryValid);
        var description = Clean(req.Description, 1000, out var descriptionValid);
        if (!regionValid || !countryValid || !descriptionValid)
        {
            error = "One or more farm fields exceed the maximum length.";
            return false;
        }

        values = new FarmValues(name, code, region, country, description);
        return true;
    }

    private static string? Clean(string? value, int maxLength, out bool valid)
    {
        var cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        valid = cleaned == null || cleaned.Length <= maxLength;
        return valid ? cleaned : null;
    }
}

public readonly record struct FarmValues(
    string Name,
    string? Code,
    string? Region,
    string? Country,
    string? Description);
