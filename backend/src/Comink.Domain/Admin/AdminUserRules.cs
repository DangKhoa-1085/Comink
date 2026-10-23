// Quy tắc nghiệp vụ quản trị tài khoản (Giai đoạn 5 — Quản trị viên CB).
// Tách khỏi Application/Infrastructure để kiểm thử được mà không cần database.
namespace Comink.Domain.Admin;

/// <summary>Kết quả đánh giá một thao tác khóa/mở khóa tài khoản.</summary>
public enum AdminAccountAction
{
    /// <summary>Thao tác hợp lệ và cần ghi dữ liệu.</summary>
    Apply = 0,

    /// <summary>Tài khoản đã ở đúng trạng thái đích → trả thành công nhưng không ghi gì (idempotent).</summary>
    AlreadyInState,

    /// <summary>Admin không được tự khóa tài khoản của chính mình (roadmap GĐ 5, mục 5).</summary>
    CannotLockSelf
}

/// <summary>
/// Quy tắc khóa/mở khóa tài khoản do quản trị viên thực hiện.
/// Trạng thái tài khoản là **chuỗi** (`Active`, `Locked`, `Deleted`) — cùng giá trị với
/// ràng buộc `ck_users_account_status` trong database (`docs/03_database_design.md` mục 2).
/// </summary>
public static class AdminUserRules
{
    /// <summary>Tài khoản hoạt động bình thường.</summary>
    public const string ActiveStatus = "Active";

    /// <summary>Tài khoản bị quản trị viên khóa.</summary>
    public const string LockedStatus = "Locked";

    /// <summary>Tài khoản đã bị vô hiệu hóa (chưa dùng ở mức CB).</summary>
    public const string DeletedStatus = "Deleted";

    /// <summary>Độ dài tối đa của lý do khóa — khớp cột `users.locked_reason` varchar(500).</summary>
    public const int MaxLockReasonLength = 500;

    /// <summary>Tài khoản có đang bị khóa không.</summary>
    public static bool IsLocked(string? accountStatus) =>
        string.Equals(accountStatus, LockedStatus, StringComparison.Ordinal);

    /// <summary>Tài khoản có đang hoạt động bình thường không.</summary>
    public static bool IsActive(string? accountStatus) =>
        string.Equals(accountStatus, ActiveStatus, StringComparison.Ordinal);

    /// <summary>
    /// Đánh giá thao tác khóa tài khoản.
    /// Thứ tự kiểm tra: tự khóa mình → đã khóa (idempotent) → cho phép ghi.
    /// </summary>
    public static AdminAccountAction EvaluateLock(Guid adminId, Guid targetUserId, string? currentStatus)
    {
        // Kiểm tra trước tiên để không phụ thuộc trạng thái hiện tại của chính admin.
        if (adminId == targetUserId)
        {
            return AdminAccountAction.CannotLockSelf;
        }

        return IsLocked(currentStatus) ? AdminAccountAction.AlreadyInState : AdminAccountAction.Apply;
    }

    /// <summary>Đánh giá thao tác mở khóa tài khoản (idempotent khi tài khoản đang hoạt động).</summary>
    public static AdminAccountAction EvaluateUnlock(string? currentStatus) =>
        IsLocked(currentStatus) ? AdminAccountAction.Apply : AdminAccountAction.AlreadyInState;

    /// <summary>
    /// Chuẩn hóa lý do khóa: chuỗi rỗng/toàn khoảng trắng thành <c>null</c> (cột cho phép null),
    /// ngược lại cắt khoảng trắng hai đầu. Không tự cắt bớt nội dung quá dài —
    /// việc từ chối thuộc validator để client biết lỗi.
    /// </summary>
    public static string? NormalizeLockReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}
