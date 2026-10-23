// Hợp đồng và tiện ích dùng chung cho nhóm quản trị (Giai đoạn 5).
namespace Comink.Application.Admin;

/// <summary>Tên các sự kiện SignalR thuộc nhóm quản trị (khớp hợp đồng API mục 4.4).</summary>
public static class AdminEvents
{
    /// <summary>
    /// Phát tới <c>user:{id}</c> ngay sau khi admin khóa tài khoản, trước khi server ngắt kết nối.
    /// Client nhận sự kiện thì xóa phiên và đăng xuất.
    /// </summary>
    public const string AccountLocked = "AccountLocked";
}
