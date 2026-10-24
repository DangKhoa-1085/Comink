// Quy tắc hiển thị tài khoản Admin (bổ sung Giai đoạn 5).
//
// Chính sách đã chốt 2026-10-24:
// - Tài khoản có role `Admin` KHÔNG xuất hiện trong danh bạ/tìm kiếm người dùng với bất kỳ ai
//   (kể cả admin khác). Admin quản lý tài khoản ở `/api/admin/users`.
// - Hồ sơ công khai của tài khoản Admin chỉ xem được bởi chính họ hoặc một Admin khác.
// - Người dùng thường không gửi được lời mời kết bạn/theo dõi/nhắn tin (kể cả mời vào nhóm) tới
//   tài khoản Admin; với Admin khác thì vẫn cho phép để không chặn công cụ quản trị.
//
// Dùng chung cho UserDirectoryService, FriendshipService, FollowService, ConversationService,
// PostService (bài viết trên trang cá nhân). Tầng này chỉ đọc role, không trả entity ra ngoài.
using Comink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Identity;

/// <summary>Truy vấn role Admin và quy tắc ẩn tài khoản Admin khỏi người dùng thường.</summary>
internal static class AdminAccounts
{
    /// <summary>Tên role quản trị (khớp policy `AdminOnly` và seed lúc khởi động).</summary>
    public const string RoleName = "Admin";

    /// <summary>Trạng thái tài khoản được phép xuất hiện/tương tác.</summary>
    private const string ActiveAccountStatus = "Active";

    /// <summary>
    /// Tập id người dùng có role Admin — dùng làm truy vấn con (`NOT IN`/`NOT EXISTS`)
    /// để lọc ngay trong SQL, không tải danh sách admin về bộ nhớ.
    /// </summary>
    public static IQueryable<Guid> Ids(ApplicationDbContext db) =>
        from userRole in db.UserRoles
        join role in db.Roles on userRole.RoleId equals role.Id
        where role.Name == RoleName
        select userRole.UserId;

    /// <summary>Người dùng có role Admin hay không.</summary>
    public static Task<bool> IsAdminAsync(
        ApplicationDbContext db,
        Guid userId,
        CancellationToken cancellationToken) =>
        Ids(db).AnyAsync(id => id == userId, cancellationToken);

    /// <summary>
    /// Người xem có được xem hồ sơ/nội dung của người đích không.
    /// Tài khoản thường luôn xem được; tài khoản Admin chỉ hiện với chính họ hoặc Admin khác.
    /// </summary>
    public static async Task<bool> IsVisibleToAsync(
        ApplicationDbContext db,
        Guid viewerId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (viewerId == targetUserId)
        {
            return true;
        }

        // Chỉ kiểm tra role người xem khi đích thực sự là Admin (tránh truy vấn thừa).
        if (!await IsAdminAsync(db, targetUserId, cancellationToken))
        {
            return true;
        }

        return await IsAdminAsync(db, viewerId, cancellationToken);
    }

    /// <summary>
    /// Người đích tồn tại, đang hoạt động **và** người gửi được phép tương tác.
    /// Tài khoản Admin không nhận lời mời kết bạn/theo dõi/nhắn tin từ người dùng thường.
    /// Trả <c>false</c> cho cả trường hợp không tồn tại để endpoint trả 404, không lộ sự tồn tại.
    /// </summary>
    public static Task<bool> IsActiveAndInteractableAsync(
        ApplicationDbContext db,
        Guid actorId,
        Guid targetUserId,
        CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == targetUserId
                        && user.AccountStatus == ActiveAccountStatus
                        && (actorId == targetUserId
                            || !Ids(db).Contains(user.Id)
                            || Ids(db).Contains(actorId)),
                cancellationToken);
}
