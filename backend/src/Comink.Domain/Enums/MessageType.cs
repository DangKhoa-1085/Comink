// Loại tin nhắn (docs/03_database_design.md mục 4).
namespace Comink.Domain.Enums;

/// <summary>Loại nội dung tin nhắn. Giai đoạn 3 chỉ dùng <see cref="Text"/> và <see cref="Image"/>.</summary>
public enum MessageType
{
    Text = 0,
    Image = 1,
    Video = 2,
    File = 3,

    /// <summary>Tin hệ thống — [NC].</summary>
    System = 4,

    /// <summary>Tin về cuộc gọi — GĐ 4.</summary>
    Call = 5
}
