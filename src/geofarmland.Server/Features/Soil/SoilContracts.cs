using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Features.Soil;

public class SoilSampleRequest
{
    public int? FarmId { get; set; }
    public int? LandParcelId { get; set; }
    public DateOnly? SampleDate { get; set; }
    public decimal? PH { get; set; }
    public decimal? OrganicMatterPct { get; set; }
    public decimal? NitrogenPpm { get; set; }
    public decimal? PhosphorusPpm { get; set; }
    public decimal? PotassiumPpm { get; set; }
    public string? Laboratory { get; set; }
    public string? Notes { get; set; }
}

public class IrrigationEventRequest
{
    public int? FarmId { get; set; }
    public int? LandParcelId { get; set; }
    public DateOnly? EventDate { get; set; }
    public decimal? VolumeM3 { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

public sealed record SoilSampleDto(
    int Id,
    int FarmId,
    string FarmName,
    int LandParcelId,
    string LandParcelName,
    DateOnly SampleDate,
    decimal PH,
    decimal OrganicMatterPct,
    decimal? NitrogenPpm,
    decimal? PhosphorusPpm,
    decimal? PotassiumPpm,
    string? Laboratory,
    string? Notes,
    DateTime CreatedAt);

public sealed record IrrigationEventDto(
    int Id,
    int FarmId,
    string FarmName,
    int LandParcelId,
    string LandParcelName,
    DateOnly EventDate,
    decimal VolumeM3,
    int? DurationMinutes,
    string Source,
    string? Notes,
    DateTime CreatedAt);

public sealed record SoilMetricsDto(
    int Samples,
    decimal? AveragePH,
    decimal? AverageOrganicMatterPct,
    int IrrigationEventsLast30Days,
    decimal WaterVolumeLast30DaysM3);

public sealed record SoilOverviewResponse(
    bool CanManage,
    SoilMetricsDto Metrics,
    IReadOnlyList<SoilSampleDto> Samples,
    IReadOnlyList<IrrigationEventDto> IrrigationEvents);

public static class SoilMapping
{
    public static SoilSampleDto ToDto(SoilSample sample) => new(
        sample.Id,
        sample.FarmId,
        sample.Farm?.Name ?? "Unknown farm",
        sample.LandParcelId,
        sample.LandParcel?.Name ?? "Unknown parcel",
        sample.SampleDate,
        sample.PH,
        sample.OrganicMatterPct,
        sample.NitrogenPpm,
        sample.PhosphorusPpm,
        sample.PotassiumPpm,
        sample.Laboratory,
        sample.Notes,
        sample.CreatedAt);

    public static IrrigationEventDto ToDto(IrrigationEvent item) => new(
        item.Id,
        item.FarmId,
        item.Farm?.Name ?? "Unknown farm",
        item.LandParcelId,
        item.LandParcel?.Name ?? "Unknown parcel",
        item.EventDate,
        item.VolumeM3,
        item.DurationMinutes,
        item.Source.ToString(),
        item.Notes,
        item.CreatedAt);
}

internal readonly record struct SoilValues(
    int FarmId,
    int LandParcelId,
    DateOnly SampleDate,
    decimal PH,
    decimal OrganicMatterPct,
    decimal? NitrogenPpm,
    decimal? PhosphorusPpm,
    decimal? PotassiumPpm,
    string? Laboratory,
    string? Notes);

internal readonly record struct IrrigationValues(
    int FarmId,
    int LandParcelId,
    DateOnly EventDate,
    decimal VolumeM3,
    int? DurationMinutes,
    IrrigationSource Source,
    string? Notes);

internal static class SoilValidation
{
    public static bool TryNormalizeSample(
        SoilSampleRequest req,
        out SoilValues values,
        out string error)
    {
        values = default;
        error = string.Empty;

        if (!req.FarmId.HasValue || !req.LandParcelId.HasValue)
        {
            error = "Select the farm and land parcel for this sample.";
            return false;
        }
        if (!req.SampleDate.HasValue)
        {
            error = "Enter the sample date.";
            return false;
        }
        if (req.SampleDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            error = "A soil sample date cannot be in the future.";
            return false;
        }
        if (!req.PH.HasValue || req.PH.Value is < 0m or > 14m)
        {
            error = "Soil pH must be between 0 and 14.";
            return false;
        }
        if (!req.OrganicMatterPct.HasValue || req.OrganicMatterPct.Value is < 0m or > 100m)
        {
            error = "Organic matter must be between 0 and 100 percent.";
            return false;
        }
        if (req.NitrogenPpm is < 0m || req.PhosphorusPpm is < 0m || req.PotassiumPpm is < 0m)
        {
            error = "Nutrient values cannot be negative.";
            return false;
        }

        var laboratory = Clean(req.Laboratory, 160, out var labValid);
        var notes = Clean(req.Notes, 1000, out var notesValid);
        if (!labValid || !notesValid)
        {
            error = "One or more soil sample fields exceed the maximum length.";
            return false;
        }

        values = new SoilValues(
            req.FarmId.Value,
            req.LandParcelId.Value,
            req.SampleDate.Value,
            req.PH.Value,
            req.OrganicMatterPct.Value,
            req.NitrogenPpm,
            req.PhosphorusPpm,
            req.PotassiumPpm,
            laboratory,
            notes);
        return true;
    }

    public static bool TryNormalizeIrrigation(
        IrrigationEventRequest req,
        out IrrigationValues values,
        out string error)
    {
        values = default;
        error = string.Empty;

        if (!req.FarmId.HasValue || !req.LandParcelId.HasValue)
        {
            error = "Select the farm and land parcel for this irrigation event.";
            return false;
        }
        if (!req.EventDate.HasValue)
        {
            error = "Enter the irrigation date.";
            return false;
        }
        if (req.EventDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            error = "An irrigation date cannot be in the future.";
            return false;
        }
        if (!req.VolumeM3.HasValue || req.VolumeM3.Value <= 0m)
        {
            error = "Enter a water volume greater than zero.";
            return false;
        }
        if (req.DurationMinutes is <= 0)
        {
            error = "Irrigation duration must be greater than zero.";
            return false;
        }

        var source = IrrigationSource.Borehole;
        if (!string.IsNullOrWhiteSpace(req.Source) && !Enum.TryParse(req.Source, true, out source))
        {
            error = "Select a valid irrigation source.";
            return false;
        }

        var notes = Clean(req.Notes, 1000, out var notesValid);
        if (!notesValid)
        {
            error = "Irrigation notes cannot exceed 1000 characters.";
            return false;
        }

        values = new IrrigationValues(
            req.FarmId.Value,
            req.LandParcelId.Value,
            req.EventDate.Value,
            req.VolumeM3.Value,
            req.DurationMinutes,
            source,
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
