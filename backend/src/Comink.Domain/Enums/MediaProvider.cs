// Enum MediaProvider — nơi lưu tệp (docs/03_database_design.md mục 4).
namespace Comink.Domain.Enums;

/// <summary>
/// Nhà cung cấp lưu trữ tài sản media.
/// `Local` chỉ dùng khi chạy dev mà chưa có khoá Cloudinary (phương án B của GĐ 2).
/// </summary>
public enum MediaProvider
{
    Cloudinary,
    Firebase,

    /// <summary>Đĩa cục bộ của máy dev — không dùng cho production.</summary>
    Local
}
