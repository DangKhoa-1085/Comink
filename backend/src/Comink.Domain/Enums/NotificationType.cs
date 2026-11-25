// Loại thông báo trong bảng `notifications` (docs/03_database_design.md mục 4).
namespace Comink.Domain.Enums;

/// <summary>Loại thông báo xã hội/cuộc gọi; tin nhắn mới không tạo dòng trong bảng này.</summary>
public enum NotificationType
{
    FriendRequestReceived,
    FriendRequestAccepted,
    NewFollower,
    PostLiked,
    PostCommented,
    CommentReplied,
    PostShared,
    Mentioned,
    MissedCall,
    AddedToGroup,
    AdminAction,
}
