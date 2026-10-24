using Geofarmland.Server.Domain.Enums;

namespace Geofarmland.Server.Domain.Entities;

public class FinancialRecord : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int? FarmId { get; set; }
    public Farm? Farm { get; set; }
    public FinancialRecordType RecordType { get; set; } = FinancialRecordType.Expense;
    public required string Category { get; set; }
    public decimal Amount { get; set; }
    public DateOnly RecordDate { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
