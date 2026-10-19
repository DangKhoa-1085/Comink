// Entity ánh xạ bảng `conversations` (docs/03_database_design.md mục 3.3).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Hội thoại. Giai đoạn 3 chỉ dùng <see cref="ConversationType.Direct"/>: mỗi cặp người
/// có đúng một hội thoại, xác định bằng <see cref="DirectKey"/>.
/// </summary>
public class Conversation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public ConversationType Type { get; set; } = ConversationType.Direct;

    /// <summary>Chỉ có khi <see cref="Type"/> = <c>Direct</c>: `{idNhỏ}_{idLớn}` (xem <c>ConversationRules</c>).</summary>
    public string? DirectKey { get; set; }

    /// <summary>Tên nhóm — [NC], chưa dùng ở GĐ 3.</summary>
    public string? Title { get; set; }

    public string? AvatarUrl { get; set; }

    public Guid CreatedBy { get; set; }
    public ApplicationUser Creator { get; set; } = null!;

    /// <summary>Tin mới nhất, dùng để xem trước và sắp xếp danh sách hội thoại.</summary>
    public Guid? LastMessageId { get; set; }
    public Message? LastMessage { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
}
