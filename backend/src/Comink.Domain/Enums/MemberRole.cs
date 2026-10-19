// Vai trò thành viên hội thoại (docs/03_database_design.md mục 4).
namespace Comink.Domain.Enums;

/// <summary>Vai trò trong hội thoại. Chat 1–1 luôn là <see cref="Member"/>.</summary>
public enum MemberRole
{
    /// <summary>Chủ nhóm — [NC] ở GĐ 3.</summary>
    Owner = 0,

    /// <summary>Quản trị nhóm — [NC] ở GĐ 3.</summary>
    Admin = 1,

    /// <summary>Thành viên thường.</summary>
    Member = 2
}
