// Enum FriendshipStatus — trạng thái lời mời kết bạn (docs/03_database_design.md mục 4).
namespace Comink.Domain.Enums;

/// <summary>
/// Trạng thái của một dòng `friendships`. Từ chối/hủy lời mời là **xóa dòng**,
/// không có trạng thái `Declined` (để người bị từ chối không bị "đánh dấu").
/// </summary>
public enum FriendshipStatus
{
    /// <summary>Đã gửi lời mời, chờ người kia phản hồi.</summary>
    Pending,

    /// <summary>Đã là bạn bè hai chiều.</summary>
    Accepted
}
