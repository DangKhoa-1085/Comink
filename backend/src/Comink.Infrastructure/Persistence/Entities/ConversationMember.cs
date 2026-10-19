// Entity ánh xạ bảng `conversation_members` (docs/03_database_design.md mục 3.3).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Thành viên hội thoại. Rời nhóm thì **giữ dòng** và đặt <see cref="LeftAt"/> để còn lịch sử
/// — nhưng rời nhóm là [NC], GĐ 3 chỉ có chat 1–1 nên không dùng.
/// </summary>
public class ConversationMember
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public MemberRole Role { get; set; } = MemberRole.Member;

    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>null = còn trong hội thoại. GĐ 3 luôn null.</summary>
    public DateTimeOffset? LeftAt { get; set; }
}
