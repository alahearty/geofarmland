using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Crops;

public class CreateHarvestEndpoint : Endpoint<HarvestRequest, HarvestDto>
{
    private readonly ApplicationDbContext _db;

    public CreateHarvestEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/crop-seasons/{id}/harvests");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(HarvestRequest req, CancellationToken ct)
    {
        var seasonId = Route<int>("id");
        var season = await _db.CropSeasons
            .Include(item => item.Crop)
            .Include(item => item.Farm)
            .Include(item => item.LandParcel)
            .FirstOrDefaultAsync(item => item.Id == seasonId, ct);
        if (season == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var access = await OrganizationAccess.ResolveAsync(_db, User, season.OrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!access.CanManageParcels)
        {
            ThrowError("Your organization role cannot record harvests.", 403);
            return;
        }

        if (!req.HarvestDate.HasValue)
        {
            ThrowError("Enter the harvest date.");
            return;
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (req.HarvestDate.Value > today)
        {
            ThrowError("A harvest date cannot be in the future.");
            return;
        }
        if (req.HarvestDate.Value < season.PlantingDate)
        {
            ThrowError("A harvest date cannot be before the planting date.");
            return;
        }
        if (!req.QuantityTons.HasValue || req.QuantityTons.Value <= 0m)
        {
            ThrowError("Enter a harvested quantity greater than zero.");
            return;
        }
        if (req.QuantityTons.Value > 1_000_000m)
        {
            ThrowError("The harvested quantity is outside the supported range.");
            return;
        }

        HarvestGrade? grade = null;
        if (!string.IsNullOrWhiteSpace(req.Grade))
        {
            if (!Enum.TryParse<HarvestGrade>(req.Grade, true, out var parsedGrade))
            {
                ThrowError("Select a valid harvest grade.");
                return;
            }
            grade = parsedGrade;
        }

        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        if (notes is { Length: > 1000 })
        {
            ThrowError("Harvest notes cannot exceed 1000 characters.");
            return;
        }

        var harvest = new HarvestRecord
        {
            OrganizationId = season.OrganizationId,
            CropSeasonId = season.Id,
            CropSeason = season,
            HarvestDate = req.HarvestDate.Value,
            QuantityTons = req.QuantityTons.Value,
            Grade = grade,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
        season.ActualHarvestDate ??= req.HarvestDate.Value;
        season.UpdatedAt = DateTime.UtcNow;
        _db.HarvestRecords.Add(harvest);
        await _db.SaveChangesAsync(ct);

        await SendAsync(CropMapping.ToDto(harvest), cancellation: ct);
    }
}
