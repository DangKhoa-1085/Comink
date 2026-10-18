// Enum Visibility — phạm vi hiển thị của bài viết (docs/03_database_design.md mục 4).
namespace Comink.Domain.Enums;

/// <summary>
/// Ai được xem nội dung. Lưu dạng chuỗi trong DB (cột `varchar(16)` + CHECK).
/// </summary>
public enum Visibility
{
    /// <summary>Mọi người.</summary>
    Public,

    /// <summary>Chỉ bạn bè (và chính chủ).</summary>
    Friends,

    /// <summary>Chỉ mình tôi.</summary>
    OnlyMe
}
