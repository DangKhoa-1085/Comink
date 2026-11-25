// Entity ánh xạ bảng `message_attachments` (docs/03_database_design.md mục 3.3).
namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>Tệp đính kèm của một tin nhắn (khóa ghép message + media).</summary>
public class MessageAttachment
{
    public Guid MessageId { get; set; }
    public Message Message { get; set; } = null!;

    public Guid MediaId { get; set; }
    public MediaAsset Media { get; set; } = null!;

    public short SortOrder { get; set; }
}
