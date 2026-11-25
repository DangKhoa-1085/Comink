// Loại hội thoại (docs/03_database_design.md mục 4).
namespace Comink.Domain.Enums;

/// <summary>Loại hội thoại. Giai đoạn 3 (CB) chỉ dùng <see cref="Direct"/>.</summary>
public enum ConversationType
{
    /// <summary>Chat riêng 1–1.</summary>
    Direct = 0,

    /// <summary>Chat nhóm — [NC], chưa dùng ở GĐ 3.</summary>
    Group = 1
}
