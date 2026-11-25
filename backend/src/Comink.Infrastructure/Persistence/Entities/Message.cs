// Entity ánh xạ bảng `messages` (docs/03_database_design.md mục 3.3).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Tin nhắn. `client_message_id` do client sinh để chống gửi trùng khi mất mạng:
/// cặp (<see cref="SenderId"/>, <see cref="ClientMessageId"/>) là duy nhất.
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public Guid SenderId { get; set; }
    public ApplicationUser Sender { get; set; } = null!;

    public MessageType Type { get; set; } = MessageType.Text;

    /// <summary>Nội dung; rỗng khi tin chỉ có ảnh (tối đa 4000 ký tự). Bị xoá khi thu hồi.</summary>
    public string? Content { get; set; }

    /// <summary>Khoá chống trùng do client sinh.</summary>
    public Guid ClientMessageId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Cuộc gọi gắn với tin nhắn loại <c>Call</c> (M3). Null với tin thường.
    /// </summary>
    public Guid? CallId { get; set; }
    public Call? Call { get; set; }

    /// <summary>Tin được phản hồi (NC Ưu tiên 2, M7). Xoá tin đích thì chỉ mất trích dẫn.</summary>
    public Guid? ReplyToMessageId { get; set; }
    public Message? ReplyToMessage { get; set; }

    /// <summary>Tin gốc của một tin chuyển tiếp (NC Ưu tiên 2, M7).</summary>
    public Guid? ForwardedFromMessageId { get; set; }
    public Message? ForwardedFromMessage { get; set; }

    /// <summary>Lần chỉnh sửa gần nhất; null = chưa sửa.</summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>Thời điểm thu hồi; null = còn hiệu lực. Tin bị thu hồi giữ dòng để hiện khung.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();

    /// <summary>Cảm xúc trên tin (NC Ưu tiên 2) — xoá kèm tin.</summary>
    public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
}
