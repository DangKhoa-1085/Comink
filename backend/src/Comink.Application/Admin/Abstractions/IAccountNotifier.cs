// Cổng phát sự kiện quản trị tới client — cài đặt bằng SignalR ở tầng Api
// (Infrastructure không được tham chiếu SignalR).
namespace Comink.Application.Admin.Abstractions;

/// <summary>Thông báo cho người dùng rằng tài khoản của họ vừa bị thay đổi trạng thái.</summary>
public interface IAccountNotifier
{
    /// <summary>
    /// Gửi <c>AccountLocked</c> tới <c>user:{id}</c> và **ngắt** mọi kết nối SignalR đang mở của
    /// người dùng (Giai đoạn 5, quyết định D2). Không ném lỗi nếu người dùng đang offline.
    /// </summary>
    Task AccountLockedAsync(Guid userId, CancellationToken cancellationToken = default);
}
