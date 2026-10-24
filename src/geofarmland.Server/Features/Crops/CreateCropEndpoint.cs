using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Crops;

public class CreateCropEndpoint : Endpoint<CropRequest, CropDto>
{
    private readonly ApplicationDbContext _db;

    public CreateCropEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/crops");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CropRequest req, CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot manage crops.", 403);
            return;
        }
        if (!CropValidation.TryNormalizeCrop(req, out var values, out var error))
        {
            ThrowError(error);
            return;
        }

        var duplicate = await _db.Crops.AnyAsync(crop =>
            crop.OrganizationId == access.Organization.Id
            && crop.Name.ToLower() == values.Name.ToLower()
            && crop.Variety == values.Variety, ct);
        if (duplicate)
        {
            ThrowError("That crop and variety already exist in this organization.");
            return;
        }

        var entity = new Crop
        {
            OrganizationId = access.Organization.Id,
            Name = values.Name,
            Category = values.Category,
            Variety = values.Variety,
            Description = values.Description,
            IsActive = values.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        _db.Crops.Add(entity);
        await _db.SaveChangesAsync(ct);

        await SendAsync(CropMapping.ToDto(entity), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
