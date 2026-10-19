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

    /// <summary>Nội dung; rỗng khi tin chỉ có ảnh (tối đa 4000 ký tự).</summary>
    public string? Content { get; set; }

    /// <summary>Khoá chống trùng do client sinh.</summary>
    public Guid ClientMessageId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Cuộc gọi gắn với tin nhắn loại <c>Call</c> (M3). Null với tin thường.
    /// </summary>
    public Guid? CallId { get; set; }
    public Call? Call { get; set; }

    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}
