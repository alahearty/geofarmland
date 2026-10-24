using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Features.Crops;

public class CropRequest
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Variety { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}

public class CropSeasonRequest
{
    public int? CropId { get; set; }
    public int? FarmId { get; set; }
    public int? LandParcelId { get; set; }
    public string? SeasonName { get; set; }
    public string? Status { get; set; }
    public DateOnly? PlantingDate { get; set; }
    public DateOnly? ExpectedHarvestDate { get; set; }
    public decimal? AreaHectares { get; set; }
    public decimal? ExpectedYieldTons { get; set; }
    public string? Notes { get; set; }
}

public class HarvestRequest
{
    public DateOnly? HarvestDate { get; set; }
    public decimal? QuantityTons { get; set; }
    public string? Grade { get; set; }
    public string? Notes { get; set; }
}

public sealed record CropDto(
    int Id,
    string Name,
    string Category,
    string? Variety,
    string? Description,
    bool IsActive,
    int SeasonCount,
    DateTime CreatedAt);

public sealed record CropSeasonDto(
    int Id,
    int CropId,
    string CropName,
    string? Variety,
    int FarmId,
    string FarmName,
    int LandParcelId,
    string LandParcelName,
    string SeasonName,
    string Status,
    DateOnly PlantingDate,
    DateOnly? ExpectedHarvestDate,
    DateOnly? ActualHarvestDate,
    decimal AreaHectares,
    decimal? ExpectedYieldTons,
    decimal HarvestedTons,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record HarvestDto(
    int Id,
    int CropSeasonId,
    string CropName,
    string SeasonName,
    string FarmName,
    DateOnly HarvestDate,
    decimal QuantityTons,
    string? Grade,
    string? Notes,
    DateTime CreatedAt);

public sealed record CropOverviewMetrics(
    int Crops,
    int ActiveSeasons,
    int PlannedSeasons,
    decimal PlantedHectares,
    decimal ExpectedYieldTons,
    decimal HarvestedTons,
    int UpcomingHarvests);

public sealed record CropOverviewResponse(
    bool CanManage,
    CropOverviewMetrics Metrics,
    IReadOnlyList<CropDto> Crops,
    IReadOnlyList<CropSeasonDto> Seasons,
    IReadOnlyList<HarvestDto> RecentHarvests);

public static class CropMapping
{
    public static CropDto ToDto(Crop crop) => new(
        crop.Id,
        crop.Name,
        crop.Category.ToString(),
        crop.Variety,
        crop.Description,
        crop.IsActive,
        crop.Seasons.Count,
        crop.CreatedAt);

    public static CropSeasonDto ToDto(CropSeason season) => new(
        season.Id,
        season.CropId,
        season.Crop?.Name ?? "Unknown crop",
        season.Crop?.Variety,
        season.FarmId,
        season.Farm?.Name ?? "Unknown farm",
        season.LandParcelId,
        season.LandParcel?.Name ?? "Unknown parcel",
        season.SeasonName,
        season.Status.ToString(),
        season.PlantingDate,
        season.ExpectedHarvestDate,
        season.ActualHarvestDate,
        season.AreaHectares,
        season.ExpectedYieldTons,
        season.Harvests.Sum(harvest => harvest.QuantityTons),
        season.Notes,
        season.CreatedAt,
        season.UpdatedAt);

    public static HarvestDto ToDto(HarvestRecord harvest) => new(
        harvest.Id,
        harvest.CropSeasonId,
        harvest.CropSeason?.Crop?.Name ?? "Unknown crop",
        harvest.CropSeason?.SeasonName ?? "Unknown season",
        harvest.CropSeason?.Farm?.Name ?? "Unknown farm",
        harvest.HarvestDate,
        harvest.QuantityTons,
        harvest.Grade?.ToString(),
        harvest.Notes,
        harvest.CreatedAt);
}

internal readonly record struct CropValues(
    string Name,
    CropCategory Category,
    string? Variety,
    string? Description,
    bool IsActive);

internal readonly record struct CropSeasonValues(
    int CropId,
    int FarmId,
    int LandParcelId,
    string SeasonName,
    CropSeasonStatus Status,
    DateOnly PlantingDate,
    DateOnly? ExpectedHarvestDate,
    decimal AreaHectares,
    decimal? ExpectedYieldTons,
    string? Notes);

internal static class CropValidation
{
    public static bool TryNormalizeCrop(CropRequest req, out CropValues values, out string error)
    {
        values = default;
        error = string.Empty;

        var name = req.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120)
        {
            error = "A crop name between 2 and 120 characters is required.";
            return false;
        }

        var category = CropCategory.Other;
        if (!string.IsNullOrWhiteSpace(req.Category) && !Enum.TryParse(req.Category, true, out category))
        {
            error = "Select a valid crop category.";
            return false;
        }

        var variety = Clean(req.Variety, 120, out var varietyValid);
        var description = Clean(req.Description, 1000, out var descriptionValid);
        if (!varietyValid || !descriptionValid)
        {
            error = "One or more crop fields exceed the maximum length.";
            return false;
        }

        values = new CropValues(name, category, variety, description, req.IsActive ?? true);
        return true;
    }

    public static bool TryNormalizeSeason(
        CropSeasonRequest req,
        LandParcel parcel,
        out CropSeasonValues values,
        out string error)
    {
        values = default;
        error = string.Empty;

        if (!req.CropId.HasValue || !req.FarmId.HasValue || !req.LandParcelId.HasValue)
        {
            error = "Select a crop, farm and land parcel for this season.";
            return false;
        }

        var seasonName = req.SeasonName?.Trim() ?? string.Empty;
        if (seasonName.Length is < 2 or > 80)
        {
            error = "A season name between 2 and 80 characters is required.";
            return false;
        }

        var status = CropSeasonStatus.Planned;
        if (!string.IsNullOrWhiteSpace(req.Status) && !Enum.TryParse(req.Status, true, out status))
        {
            error = "Select a valid crop season status.";
            return false;
        }

        if (!req.PlantingDate.HasValue)
        {
            error = "Enter the planting date.";
            return false;
        }
        if (req.ExpectedHarvestDate.HasValue && req.ExpectedHarvestDate.Value < req.PlantingDate.Value)
        {
            error = "The expected harvest date cannot be before the planting date.";
            return false;
        }

        var area = req.AreaHectares ?? parcel.AreaHectares;
        if (area <= 0m)
        {
            error = "The planted area must be greater than zero.";
            return false;
        }
        if (parcel.AreaHectares > 0m && area > parcel.AreaHectares + 0.01m)
        {
            error = $"The planted area cannot exceed the parcel area of {parcel.AreaHectares:0.####} ha.";
            return false;
        }

        if (req.ExpectedYieldTons is < 0m)
        {
            error = "Expected yield cannot be negative.";
            return false;
        }

        var notes = Clean(req.Notes, 1000, out var notesValid);
        if (!notesValid)
        {
            error = "Season notes cannot exceed 1000 characters.";
            return false;
        }

        values = new CropSeasonValues(
            req.CropId.Value,
            req.FarmId.Value,
            req.LandParcelId.Value,
            seasonName,
            status,
            req.PlantingDate.Value,
            req.ExpectedHarvestDate,
            area,
            req.ExpectedYieldTons,
            notes);
        return true;
    }

    private static string? Clean(string? value, int maxLength, out bool valid)
    {
        var cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        valid = cleaned == null || cleaned.Length <= maxLength;
        return valid ? cleaned : null;
    }
}
