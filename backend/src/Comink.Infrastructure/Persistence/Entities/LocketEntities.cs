// Entity ánh xạ các bảng `locket_moments`, `locket_recipients`, `locket_reactions`.
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>Một khoảnh khắc Locket có thời hạn 24 giờ kể từ thời điểm đăng.</summary>
public class LocketMoment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;

    public Guid MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;

    /// <summary>Chú thích ngắn nổi trên ảnh (tối đa 100 ký tự).</summary>
    public string? Caption { get; set; }

    public LocketAudience AudienceType { get; set; } = LocketAudience.AllFriends;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Thời điểm hết hạn (CreatedAt + 24 giờ).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Xóa mềm khi chủ sở hữu xóa sớm hoặc khi hết hạn 24 giờ.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<LocketRecipient> Recipients { get; set; } = new List<LocketRecipient>();
    public ICollection<LocketReaction> Reactions { get; set; } = new List<LocketReaction>();
}

/// <summary>Danh sách người nhận cụ thể khi Locket chọn gửi tới nhóm bạn thân.</summary>
public class LocketRecipient
{
    public Guid MomentId { get; set; }
    public LocketMoment Moment { get; set; } = null!;

    public Guid RecipientUserId { get; set; }
    public ApplicationUser RecipientUser { get; set; } = null!;
}

/// <summary>Cảm xúc của một người dùng trên khoảnh khắc Locket (mỗi người tối đa 1 cảm xúc/ảnh).</summary>
public class LocketReaction
{
    public Guid MomentId { get; set; }
    public LocketMoment Moment { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string Emoji { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
