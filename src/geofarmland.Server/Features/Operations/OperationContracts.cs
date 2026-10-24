using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Features.Operations;

public class FarmTaskRequest
{
    public int? FarmId { get; set; }
    public int? LandParcelId { get; set; }
    public string? Title { get; set; }
    public string? TaskType { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Notes { get; set; }
}

public sealed record FarmTaskDto(
    int Id,
    int FarmId,
    string FarmName,
    int? LandParcelId,
    string? LandParcelName,
    string Title,
    string TaskType,
    string Priority,
    string Status,
    DateOnly? DueDate,
    DateTime? CompletedAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public bool IsOverdue => DueDate.HasValue
        && DueDate.Value < DateOnly.FromDateTime(DateTime.UtcNow)
        && Status is "Pending" or "InProgress";
}

public sealed record OperationsMetricsDto(
    int OpenTasks,
    int InProgress,
    int Overdue,
    int DueThisWeek,
    int HighPriority,
    int CompletedLast30Days);

public sealed record OperationsOverviewResponse(
    bool CanManage,
    OperationsMetricsDto Metrics,
    IReadOnlyList<FarmTaskDto> Tasks);

public static class FarmTaskMapping
{
    public static FarmTaskDto ToDto(FarmTask task) => new(
        task.Id,
        task.FarmId,
        task.Farm?.Name ?? "Unknown farm",
        task.LandParcelId,
        task.LandParcel?.Name,
        task.Title,
        task.TaskType.ToString(),
        task.Priority.ToString(),
        task.Status.ToString(),
        task.DueDate,
        task.CompletedAt,
        task.Notes,
        task.CreatedAt,
        task.UpdatedAt);
}

internal readonly record struct FarmTaskValues(
    int FarmId,
    int? LandParcelId,
    string Title,
    FarmTaskType TaskType,
    FarmTaskPriority Priority,
    FarmTaskStatus Status,
    DateOnly? DueDate,
    string? Notes);

internal static class FarmTaskValidation
{
    public static bool TryNormalize(FarmTaskRequest req, out FarmTaskValues values, out string error)
    {
        values = default;
        error = string.Empty;

        if (!req.FarmId.HasValue)
        {
            error = "Select the farm for this task.";
            return false;
        }

        var title = req.Title?.Trim() ?? string.Empty;
        if (title.Length is < 2 or > 160)
        {
            error = "A task title between 2 and 160 characters is required.";
            return false;
        }

        var taskType = FarmTaskType.Inspection;
        if (!string.IsNullOrWhiteSpace(req.TaskType) && !Enum.TryParse(req.TaskType, true, out taskType))
        {
            error = "Select a valid task type.";
            return false;
        }

        var priority = FarmTaskPriority.Medium;
        if (!string.IsNullOrWhiteSpace(req.Priority) && !Enum.TryParse(req.Priority, true, out priority))
        {
            error = "Select a valid task priority.";
            return false;
        }

        var status = FarmTaskStatus.Pending;
        if (!string.IsNullOrWhiteSpace(req.Status) && !Enum.TryParse(req.Status, true, out status))
        {
            error = "Select a valid task status.";
            return false;
        }

        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        if (notes is { Length: > 2000 })
        {
            error = "Task notes cannot exceed 2000 characters.";
            return false;
        }

        values = new FarmTaskValues(
            req.FarmId.Value,
            req.LandParcelId,
            title,
            taskType,
            priority,
            status,
            req.DueDate,
            notes);
        return true;
    }
}
