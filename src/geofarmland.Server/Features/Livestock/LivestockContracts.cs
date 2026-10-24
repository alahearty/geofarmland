using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Features.Livestock;

public class LivestockGroupRequest
{
    public int? FarmId { get; set; }
    public int? GrazingLandParcelId { get; set; }
    public string? Name { get; set; }
    public string? Species { get; set; }
    public string? Breed { get; set; }
    public string? Purpose { get; set; }
    public int? HeadCount { get; set; }
    public decimal? AverageWeightKg { get; set; }
    public string? Status { get; set; }
    public DateOnly? JoinedDate { get; set; }
    public string? Notes { get; set; }
}

public class LivestockEventRequest
{
    public string? EventType { get; set; }
    public DateOnly? EventDate { get; set; }
    public int? HeadCount { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Notes { get; set; }
}

public sealed record LivestockGroupDto(
    int Id,
    int FarmId,
    string FarmName,
    int? GrazingLandParcelId,
    string? GrazingParcelName,
    string Name,
    string Species,
    string? Breed,
    string Purpose,
    int HeadCount,
    decimal? AverageWeightKg,
    string Status,
    DateOnly? JoinedDate,
    string? Notes,
    int EventCount,
    DateOnly? LastEventDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record LivestockEventDto(
    int Id,
    int LivestockGroupId,
    string GroupName,
    string FarmName,
    string EventType,
    DateOnly EventDate,
    int? HeadCount,
    decimal? WeightKg,
    string? Notes,
    DateTime CreatedAt);

public sealed record SpeciesBreakdownDto(string Species, int Groups, int HeadCount);

public sealed record LivestockMetricsDto(
    int TotalHead,
    int ActiveGroups,
    int QuarantineGroups,
    int EventsLast30Days,
    decimal? AverageWeightKg);

public sealed record LivestockOverviewResponse(
    bool CanManage,
    LivestockMetricsDto Metrics,
    IReadOnlyList<LivestockGroupDto> Groups,
    IReadOnlyList<LivestockEventDto> RecentEvents,
    IReadOnlyList<SpeciesBreakdownDto> SpeciesBreakdown);

public static class LivestockMapping
{
    public static LivestockGroupDto ToDto(LivestockGroup group)
    {
        var events = group.Events;
        return new LivestockGroupDto(
            group.Id,
            group.FarmId,
            group.Farm?.Name ?? "Unknown farm",
            group.GrazingLandParcelId,
            group.GrazingLandParcel?.Name,
            group.Name,
            group.Species.ToString(),
            group.Breed,
            group.Purpose.ToString(),
            group.HeadCount,
            group.AverageWeightKg,
            group.Status.ToString(),
            group.JoinedDate,
            group.Notes,
            events.Count,
            events.Count == 0 ? null : events.Max(item => item.EventDate),
            group.CreatedAt,
            group.UpdatedAt);
    }

    public static LivestockEventDto ToDto(LivestockEvent item) => new(
        item.Id,
        item.LivestockGroupId,
        item.LivestockGroup?.Name ?? "Unknown group",
        item.LivestockGroup?.Farm?.Name ?? "Unknown farm",
        item.EventType.ToString(),
        item.EventDate,
        item.HeadCount,
        item.WeightKg,
        item.Notes,
        item.CreatedAt);
}

internal readonly record struct LivestockGroupValues(
    int FarmId,
    int? GrazingLandParcelId,
    string Name,
    LivestockSpecies Species,
    string? Breed,
    LivestockPurpose Purpose,
    int HeadCount,
    decimal? AverageWeightKg,
    LivestockStatus Status,
    DateOnly? JoinedDate,
    string? Notes);

internal static class LivestockValidation
{
    public static bool TryNormalizeGroup(
        LivestockGroupRequest req,
        out LivestockGroupValues values,
        out string error)
    {
        values = default;
        error = string.Empty;

        if (!req.FarmId.HasValue)
        {
            error = "Select the farm that owns this livestock group.";
            return false;
        }

        var name = req.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120)
        {
            error = "A livestock group name between 2 and 120 characters is required.";
            return false;
        }

        var species = LivestockSpecies.Cattle;
        if (!string.IsNullOrWhiteSpace(req.Species) && !Enum.TryParse(req.Species, true, out species))
        {
            error = "Select a valid livestock species.";
            return false;
        }

        var purpose = LivestockPurpose.Beef;
        if (!string.IsNullOrWhiteSpace(req.Purpose) && !Enum.TryParse(req.Purpose, true, out purpose))
        {
            error = "Select a valid livestock purpose.";
            return false;
        }

        var status = LivestockStatus.Active;
        if (!string.IsNullOrWhiteSpace(req.Status) && !Enum.TryParse(req.Status, true, out status))
        {
            error = "Select a valid livestock status.";
            return false;
        }

        if (!req.HeadCount.HasValue || req.HeadCount.Value is < 1 or > 1_000_000)
        {
            error = "Head count must be between 1 and 1,000,000.";
            return false;
        }

        if (req.AverageWeightKg is < 0m or > 10_000m)
        {
            error = "Average weight must be between 0 and 10,000 kg.";
            return false;
        }

        if (req.JoinedDate.HasValue && req.JoinedDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            error = "The joined date cannot be in the future.";
            return false;
        }

        var breed = Clean(req.Breed, 120, out var breedValid);
        var notes = Clean(req.Notes, 1000, out var notesValid);
        if (!breedValid || !notesValid)
        {
            error = "One or more livestock group fields exceed the maximum length.";
            return false;
        }

        values = new LivestockGroupValues(
            req.FarmId.Value,
            req.GrazingLandParcelId,
            name,
            species,
            breed,
            purpose,
            req.HeadCount.Value,
            req.AverageWeightKg,
            status,
            req.JoinedDate,
            notes);
        return true;
    }

    public static bool TryNormalizeEvent(
        LivestockEventRequest req,
        LivestockGroup group,
        out LivestockEventType eventType,
        out string error)
    {
        eventType = LivestockEventType.Health;
        error = string.Empty;

        if (!string.IsNullOrWhiteSpace(req.EventType)
            && !Enum.TryParse(req.EventType, true, out eventType))
        {
            error = "Select a valid livestock event type.";
            return false;
        }

        if (!req.EventDate.HasValue)
        {
            error = "Enter the event date.";
            return false;
        }
        if (req.EventDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            error = "A livestock event cannot be dated in the future.";
            return false;
        }

        if (req.HeadCount is < 1)
        {
            error = "The affected head count must be greater than zero.";
            return false;
        }
        if (req.HeadCount.HasValue && req.HeadCount.Value > group.HeadCount)
        {
            error = "The affected head count cannot exceed the group's current head count.";
            return false;
        }

        if (req.WeightKg is <= 0m or > 10_000m)
        {
            error = "Weight must be between 0 and 10,000 kg.";
            return false;
        }

        if (eventType == LivestockEventType.Weight && !req.WeightKg.HasValue)
        {
            error = "Enter the weight for a weight event.";
            return false;
        }

        if (req.Notes?.Trim().Length > 1000)
        {
            error = "Event notes cannot exceed 1000 characters.";
            return false;
        }

        return true;
    }

    private static string? Clean(string? value, int maxLength, out bool valid)
    {
        var cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        valid = cleaned == null || cleaned.Length <= maxLength;
        return valid ? cleaned : null;
    }
}
