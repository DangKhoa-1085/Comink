// Entity ánh xạ bảng `pinned_messages` (docs/03_database_design.md mục 3.3).
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Một tin được ghim trong hội thoại. Giới hạn 10 tin/hội thoại kiểm tra ở tầng ứng dụng
/// (docs/03 mục 7 #4).
/// </summary>
public class PinnedMessage
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public Guid MessageId { get; set; }
    public Message Message { get; set; } = null!;

    public Guid PinnedBy { get; set; }
    public ApplicationUser PinnedByUser { get; set; } = null!;

    public DateTimeOffset PinnedAt { get; set; } = DateTimeOffset.UtcNow;
}
