// Entity ánh xạ bảng `message_reactions` (docs/03_database_design.md mục 3.3).
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Một cảm xúc của một người trên một tin. Khoá chính gồm cả <see cref="Emoji"/> nên
/// **một người được thả nhiều loại cảm xúc** lên cùng một tin (docs/03 mục 7 #1).
/// </summary>
public class MessageReaction
{
    public Guid MessageId { get; set; }
    public Message Message { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Ký tự emoji (không phải khoá API). Ví dụ ❤️, 👍.</summary>
    public string Emoji { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
