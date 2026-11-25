// Quy tắc nghiệp vụ cho hội thoại (docs/05_api_contract.md mục 8).
namespace Comink.Domain.Messaging;

/// <summary>
/// Quy tắc hội thoại: khoá duy nhất cho chat 1–1 và kiểm tra cơ bản.
/// </summary>
public static class ConversationRules
{
    /// <summary>Độ dài tối đa của `direct_key` = 36 + 1 + 36 (docs/03_database_design.md mục 3.3).</summary>
    public const int DirectKeyMaxLength = 73;

    /// <summary>
    /// Khoá của một cặp người dùng cho chat riêng: `{idNhỏ}_{idLớn}`.
    /// <para>
    /// Phải sắp xếp **ổn định** để hai phía gọi theo thứ tự nào cũng ra cùng khoá —
    /// dùng dạng chuỗi chuẩn `"D"` và so sánh ordinal (không phụ thuộc culture).
    /// Sai chỗ này sẽ tạo ra hai hội thoại cho cùng một cặp người.
    /// </para>
    /// </summary>
    public static string BuildDirectKey(Guid first, Guid second)
    {
        var a = first.ToString("D");
        var b = second.ToString("D");

        return string.CompareOrdinal(a, b) <= 0 ? $"{a}_{b}" : $"{b}_{a}";
    }

    /// <summary>Tự mở hội thoại với chính mình là không hợp lệ.</summary>
    public static bool IsSelfConversation(Guid first, Guid second) => first == second;

    // --- Chat nhóm (NC Giai đoạn 3 — docs/tasks/phase3_messaging.md mục 7) ---

    /// <summary>Nhóm tối thiểu 3 thành viên (tính cả người tạo) — giống Messenger.</summary>
    public const int MinGroupMembers = 3;

    /// <summary>
    /// Nhóm tối đa 10 thành viên — bằng <c>CallRules.MaxParticipants</c> vì cuộc gọi nhóm
    /// dùng chính hội thoại nhóm đó.
    /// </summary>
    public const int MaxGroupMembers = 10;

    /// <summary>Tên nhóm: bắt buộc, tối đa 100 ký tự.</summary>
    public const int MaxGroupTitleLength = 100;

    /// <summary>Số thành viên (tính cả người tạo) có hợp lệ cho một nhóm không.</summary>
    public static bool IsGroupSizeValid(int memberCount) =>
        memberCount is >= MinGroupMembers and <= MaxGroupMembers;

    // --- Mốc đã đọc (NC Giai đoạn 7 — docs/tasks/phase7_advanced_messaging.md) ---

    /// <summary>
    /// Mốc đã đọc chỉ được **tiến**: chỉ nhận tin mới hơn mốc hiện tại theo đúng thứ tự con trỏ
    /// `(created_at, id)` — giống cách phân trang lịch sử tin (docs/05_api_contract.md mục 8.5).
    /// <para>
    /// Nhờ vậy hai client của cùng một người (nhiều tab) gửi mốc lệch thứ tự cũng không làm mốc
    /// bị lùi, và mở lại khung chat không phát lại sự kiện "đã đọc" vô ích.
    /// </para>
    /// </summary>
    public static bool CanAdvanceReadMarker(
        DateTimeOffset? currentReadAt,
        Guid? currentMessageId,
        DateTimeOffset messageCreatedAt,
        Guid messageId)
    {
        if (currentReadAt is null)
        {
            return true;
        }

        if (messageCreatedAt != currentReadAt.Value)
        {
            return messageCreatedAt > currentReadAt.Value;
        }

        // Cùng thời điểm (hiếm khi xảy ra nhưng vẫn có): so id, UUIDv7 tăng dần theo thời gian.
        return currentMessageId is null || messageId.CompareTo(currentMessageId.Value) > 0;
    }
}
