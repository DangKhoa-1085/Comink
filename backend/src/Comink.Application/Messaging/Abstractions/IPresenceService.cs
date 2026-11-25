// Hợp đồng presence (trạng thái online) — NC Giai đoạn 7, docs/tasks/phase7_advanced_messaging.md.
// Cài đặt dùng Redis có TTL (docs/02_tech_stack_decisions.md mục 5.2) nằm ở Infrastructure.
using Comink.Application.Messaging.Dtos;

namespace Comink.Application.Messaging.Abstractions;

/// <summary>
/// Theo dõi trạng thái online theo **từng kết nối SignalR** (nhiều tab/thiết bị) thay vì một cờ
/// duy nhất cho mỗi người. Khoá presence có TTL để client chết không gửi disconnect vẫn tự offline.
/// </summary>
/// <remarks>
/// Mọi phương thức phải **không ném lỗi khi Redis không sẵn sàng** (docs/02 mục 5.4: mất Redis
/// không được làm hỏng nghiệp vụ) — cài đặt coi như offline và chỉ ghi log.
/// </remarks>
public interface IPresenceService
{
    /// <summary>
    /// Ghi nhận một kết nối vừa mở. Trả <c>true</c> khi đây là kết nối đầu tiên (offline → online)
    /// để nơi gọi phát sự kiện <c>PresenceChanged</c>.
    /// </summary>
    Task<bool> ConnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>Làm mới TTL presence của người dùng (client gửi <c>Ping</c> định kỳ).</summary>
    Task TouchAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gỡ một kết nối. Trả thời điểm ngắt khi đây là kết nối **cuối cùng** (online → offline),
    /// <c>null</c> khi người dùng vẫn còn kết nối khác.
    /// </summary>
    Task<DateTimeOffset?> DisconnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>Trạng thái của nhiều người trong một lượt (dùng cho danh sách/chi tiết hội thoại).</summary>
    Task<IReadOnlyDictionary<Guid, PresenceDto>> GetStatusesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
