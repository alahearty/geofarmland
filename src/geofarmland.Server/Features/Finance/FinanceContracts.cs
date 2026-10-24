using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Features.Finance;

public class FinancialRecordRequest
{
    public int? FarmId { get; set; }
    public string? RecordType { get; set; }
    public string? Category { get; set; }
    public decimal? Amount { get; set; }
    public DateOnly? RecordDate { get; set; }
    public string? Description { get; set; }
}

public sealed record FinancialRecordDto(
    int Id,
    int? FarmId,
    string? FarmName,
    string RecordType,
    string Category,
    decimal Amount,
    DateOnly RecordDate,
    string? Description,
    DateTime CreatedAt);

public sealed record FinanceMetricsDto(
    decimal Revenue,
    decimal Expenses,
    decimal NetPosition,
    int Records);

public sealed record FinanceOverviewResponse(
    bool CanManage,
    FinanceMetricsDto Metrics,
    IReadOnlyList<FinancialRecordDto> Records);

public static class FinanceMapping
{
    public static FinancialRecordDto ToDto(FinancialRecord record) => new(
        record.Id,
        record.FarmId,
        record.Farm?.Name,
        record.RecordType.ToString(),
        record.Category,
        record.Amount,
        record.RecordDate,
        record.Description,
        record.CreatedAt);
}

internal readonly record struct FinancialValues(
    int? FarmId,
    FinancialRecordType RecordType,
    string Category,
    decimal Amount,
    DateOnly RecordDate,
    string? Description);

internal static class FinanceValidation
{
    public static bool TryNormalize(FinancialRecordRequest req, out FinancialValues values, out string error)
    {
        values = default;
        error = string.Empty;

        var recordType = FinancialRecordType.Expense;
        if (!string.IsNullOrWhiteSpace(req.RecordType) && !Enum.TryParse(req.RecordType, true, out recordType))
        {
            error = "Select a valid financial record type.";
            return false;
        }

        var category = req.Category?.Trim() ?? string.Empty;
        if (category.Length is < 2 or > 80)
        {
            error = "A financial category between 2 and 80 characters is required.";
            return false;
        }

        if (!req.Amount.HasValue || req.Amount.Value <= 0m)
        {
            error = "Enter an amount greater than zero.";
            return false;
        }
        if (req.Amount.Value > 1_000_000_000m)
        {
            error = "The amount is outside the supported range.";
            return false;
        }

        if (!req.RecordDate.HasValue)
        {
            error = "Enter the record date.";
            return false;
        }
        if (req.RecordDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            error = "A financial record date cannot be in the future.";
            return false;
        }

        var description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        if (description is { Length: > 500 })
        {
            error = "The description cannot exceed 500 characters.";
            return false;
        }

        values = new FinancialValues(
            req.FarmId,
            recordType,
            category,
            req.Amount.Value,
            req.RecordDate.Value,
            description);
        return true;
    }
}
