using FastEndpoints;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Crops;

public class UpdateCropEndpoint : Endpoint<CropRequest, CropDto>
{
    private readonly ApplicationDbContext _db;

    public UpdateCropEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/api/crops/{id}");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CropRequest req, CancellationToken ct)
    {
        var cropId = Route<int>("id");
        var crop = await _db.Crops
            .Include(item => item.Seasons)
            .FirstOrDefaultAsync(item => item.Id == cropId, ct);
        if (crop == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, crop.OrganizationId, ct);
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

        var duplicate = await _db.Crops.AnyAsync(item =>
            item.OrganizationId == crop.OrganizationId
            && item.Id != crop.Id
            && item.Name.ToLower() == values.Name.ToLower()
            && item.Variety == values.Variety, ct);
        if (duplicate)
        {
            ThrowError("That crop and variety already exist in this organization.");
            return;
        }

        crop.Name = values.Name;
        crop.Category = values.Category;
        crop.Variety = values.Variety;
        crop.Description = values.Description;
        crop.IsActive = values.IsActive;
        await _db.SaveChangesAsync(ct);

        await SendAsync(CropMapping.ToDto(crop), cancellation: ct);
    }
}
