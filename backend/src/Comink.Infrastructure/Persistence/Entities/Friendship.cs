// Entity ánh xạ bảng `friendships` (docs/03_database_design.md mục 3.2).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Quan hệ bạn bè hai chiều, gồm cả lời mời đang chờ.
/// Mỗi cặp người dùng chỉ có tối đa một dòng, bất kể ai gửi trước
/// (ràng buộc bằng chỉ mục duy nhất trên LEAST/GREATEST — xem cấu hình EF).
/// Từ chối, hủy lời mời hoặc hủy kết bạn = xóa dòng.
/// </summary>
public class Friendship
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Người gửi lời mời.</summary>
    public Guid RequesterId { get; set; }
    public ApplicationUser Requester { get; set; } = null!;

    /// <summary>Người nhận lời mời.</summary>
    public Guid AddresseeId { get; set; }
    public ApplicationUser Addressee { get; set; } = null!;

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    /// <summary>Thời điểm gửi lời mời.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Thời điểm chấp nhận; null khi còn chờ.</summary>
    public DateTimeOffset? RespondedAt { get; set; }
}
