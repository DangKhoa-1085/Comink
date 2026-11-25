// Lý do kết thúc cuộc gọi (docs/03_database_design.md mục 3.4).
namespace Comink.Domain.Enums;

/// <summary>Lý do kết thúc cuộc gọi, ghi vào `calls.end_reason`.</summary>
public enum CallEndReason
{
    Completed = 0,
    NoAnswer = 1,
    Declined = 2,
    Cancelled = 3,
    Failed = 4
}
