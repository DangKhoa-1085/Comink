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
}
