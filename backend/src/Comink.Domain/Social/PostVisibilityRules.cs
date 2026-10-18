// Quy tắc quyền xem bài viết — một nguồn duy nhất cho mọi đường truy cập.
using Comink.Domain.Enums;

namespace Comink.Domain.Social;

/// <summary>
/// Quy tắc xem nội dung (docs/01_COMINK_SPEC.md mục 3.1 [CB]; docs/03_database_design.md mục 4).
/// Đặt ở Domain để mọi đường truy cập (chi tiết bài, bảng tin, trang cá nhân, thích, bình luận,
/// chia sẻ) dùng chung một quy tắc, tránh mỗi endpoint tự kiểm tra một kiểu.
/// </summary>
public static class PostVisibilityRules
{
    /// <summary>Người xem có được xem bài hay không.</summary>
    /// <param name="visibility">Phạm vi hiển thị của bài.</param>
    /// <param name="isAuthor">Người xem có phải tác giả không.</param>
    /// <param name="areFriends">Hai người có phải bạn bè (FriendshipStatus = Accepted) không.</param>
    public static bool CanView(Visibility visibility, bool isAuthor, bool areFriends)
    {
        if (isAuthor)
        {
            return true;
        }

        return visibility switch
        {
            Visibility.Public => true,
            Visibility.Friends => areFriends,
            Visibility.OnlyMe => false,
            _ => false
        };
    }

    /// <summary>Bài còn hiển thị không: đã xóa mềm hoặc bị quản trị viên ẩn thì coi như không tồn tại.</summary>
    public static bool IsVisible(ContentStatus status, DateTimeOffset? deletedAt) =>
        deletedAt is null && status == ContentStatus.Active;
}
