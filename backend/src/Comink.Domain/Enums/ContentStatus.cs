// Enum ContentStatus — trạng thái kiểm duyệt của nội dung (docs/03_database_design.md mục 4).
namespace Comink.Domain.Enums;

/// <summary>
/// Trạng thái nội dung do quản trị viên kiểm soát (`posts.status`, `comments.status`).
/// </summary>
public enum ContentStatus
{
    /// <summary>Hiển thị bình thường.</summary>
    Active,

    /// <summary>Bị quản trị viên ẩn (Giai đoạn 5/7).</summary>
    Hidden
}
