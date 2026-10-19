// Chuyển entity sang DTO cho module Nhắn tin.
using Comink.Application.Messaging.Dtos;
using Comink.Infrastructure.Persistence.Entities;

namespace Comink.Infrastructure.Messaging;

internal static class MessagingMapping
{
    /// <summary>Độ dài tối đa của phần xem trước tin cuối trong danh sách hội thoại.</summary>
    private const int PreviewLength = 120;

    public static MessageDto ToMessageDto(Message message) => new(
        message.Id,
        message.ConversationId,
        message.SenderId,
        message.Type.ToString(),
        message.Content,
        message.Attachments
            .OrderBy(a => a.SortOrder)
            .Select(a => new MessageAttachmentDto(
                a.MediaId,
                a.Media.Url,
                a.Media.ThumbnailUrl,
                a.Media.Width,
                a.Media.Height))
            .ToList(),
        message.ClientMessageId,
        message.CreatedAt);

    /// <summary>
    /// Xem trước nội dung tin. Tin chỉ có ảnh trả chuỗi rỗng — client tự hiển thị nhãn "[Ảnh]"
    /// theo `type` để giữ đa ngôn ngữ ở phía client.
    /// </summary>
    public static string Preview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content.Trim().ReplaceLineEndings(" ");
        return normalized.Length <= PreviewLength ? normalized : normalized[..PreviewLength];
    }
}
