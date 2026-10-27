// Entity ánh xạ bảng `notifications` (docs/03_database_design.md mục 3.7).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>Thông báo trong app (lời mời kết bạn, tương tác, cuộc gọi nhỡ...).</summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RecipientId { get; set; }
    public ApplicationUser Recipient { get; set; } = null!;

    /// <summary>Người gây ra sự kiện; null nếu tài khoản đã bị xóa.</summary>
    public Guid? ActorId { get; set; }
    public ApplicationUser? Actor { get; set; }

    public NotificationType Type { get; set; }

    /// <summary>Post, Comment, Conversation, Call hoặc Friendship.</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>Dữ liệu hiển thị thêm, lưu dạng JSON (jsonb).</summary>
    public string? Data { get; set; }

    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
