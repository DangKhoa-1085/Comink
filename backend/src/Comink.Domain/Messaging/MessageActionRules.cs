// Quy tắc cho các thao tác trên tin nhắn (NC Giai đoạn 7 — Ưu tiên 2):
// chỉnh sửa, thu hồi, ghim (docs/tasks/phase7b_advanced_messaging.md mục 2 — D1, D3).
using Comink.Domain.Enums;

namespace Comink.Domain.Messaging;

/// <summary>Kết quả kiểm tra một thao tác trên tin nhắn; service quy đổi sang mã lỗi API.</summary>
public enum MessageActionOutcome
{
    /// <summary>Được phép thực hiện.</summary>
    Allowed,

    /// <summary>Không phải người gửi tin.</summary>
    NotOwner,

    /// <summary>Tin đã bị thu hồi.</summary>
    AlreadyDeleted,

    /// <summary>Loại tin không hỗ trợ thao tác (tin hệ thống, tin cuộc gọi).</summary>
    NotEditable,

    /// <summary>Đã quá cửa sổ thời gian cho phép (24 giờ).</summary>
    WindowExpired
}

/// <summary>
/// Quy tắc sửa/thu hồi tin: chỉ người gửi, trong <see cref="ActionWindowHours"/> giờ,
/// tin chưa bị thu hồi và không phải tin hệ thống/cuộc gọi.
/// </summary>
public static class MessageActionRules
{
    /// <summary>Số giờ được phép sửa/thu hồi kể từ lúc gửi (quyết định D1, đã chốt với người dùng).</summary>
    public const int ActionWindowHours = 24;

    /// <summary>Số tin ghim tối đa mỗi hội thoại (docs/03 mục 7 #4).</summary>
    public const int MaxPinnedMessages = 10;

    /// <summary>Cửa sổ thời gian cho phép sửa/thu hồi.</summary>
    public static readonly TimeSpan ActionWindow = TimeSpan.FromHours(ActionWindowHours);

    /// <summary>Kiểm tra quyền và thời hạn <b>chỉnh sửa</b> một tin.</summary>
    public static MessageActionOutcome EvaluateEdit(
        Guid senderId,
        Guid currentUserId,
        bool isDeleted,
        MessageType type,
        DateTimeOffset createdAt,
        DateTimeOffset now) =>
        Evaluate(senderId, currentUserId, isDeleted, type, createdAt, now);

    /// <summary>Kiểm tra quyền và thời hạn <b>thu hồi</b> một tin.</summary>
    public static MessageActionOutcome EvaluateRecall(
        Guid senderId,
        Guid currentUserId,
        bool isDeleted,
        MessageType type,
        DateTimeOffset createdAt,
        DateTimeOffset now) =>
        Evaluate(senderId, currentUserId, isDeleted, type, createdAt, now);

    /// <summary>
    /// Tin hệ thống (<see cref="MessageType.System"/>) do backend tạo cho sự kiện nhóm và
    /// tin cuộc gọi (<see cref="MessageType.Call"/>) là bản ghi lịch sử — không sửa/thu hồi.
    /// </summary>
    public static bool IsEditableType(MessageType type) =>
        type is not (MessageType.System or MessageType.Call);

    private static MessageActionOutcome Evaluate(
        Guid senderId,
        Guid currentUserId,
        bool isDeleted,
        MessageType type,
        DateTimeOffset createdAt,
        DateTimeOffset now)
    {
        // Thứ tự kiểm tra cố định để client nhận đúng lỗi ưu tiên:
        // không phải chủ sở hữu → đã thu hồi → loại tin → quá hạn.
        if (senderId != currentUserId)
        {
            return MessageActionOutcome.NotOwner;
        }

        if (isDeleted)
        {
            return MessageActionOutcome.AlreadyDeleted;
        }

        if (!IsEditableType(type))
        {
            return MessageActionOutcome.NotEditable;
        }

        // now < createdAt (lệch đồng hồ) vẫn coi là còn trong hạn.
        return now - createdAt > ActionWindow
            ? MessageActionOutcome.WindowExpired
            : MessageActionOutcome.Allowed;
    }
}
