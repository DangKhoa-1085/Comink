// Chuyển entity sang DTO cho module Nhắn tin.
using Comink.Application.Messaging.Dtos;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence.Entities;

namespace Comink.Infrastructure.Messaging;

internal static class MessagingMapping
{
    /// <summary>Độ dài tối đa của phần xem trước tin cuối trong danh sách hội thoại.</summary>
    private const int PreviewLength = 120;

    /// <summary>Độ dài tối đa của phần trích dẫn tin được phản hồi.</summary>
    private const int QuotePreviewLength = 120;

    /// <summary>DTO tin nhắn không kèm dữ liệu phụ (trích dẫn/cảm xúc) — dùng cho preview.</summary>
    public static MessageDto ToMessageDto(Message message) =>
        ToMessageDto(message, replyTo: null, forwardedFrom: null, reactions: []);

    /// <summary>
    /// DTO tin nhắn đầy đủ. Tin đã bị thu hồi thì **luôn** trả nội dung/tệp/cảm xúc/trích dẫn rỗng
    /// để client chỉ hiện khung "Tin nhắn đã bị thu hồi".
    /// </summary>
    public static MessageDto ToMessageDto(
        Message message,
        MessageQuoteDto? replyTo,
        ForwardedFromDto? forwardedFrom,
        IReadOnlyList<ReactionSummaryDto> reactions)
    {
        var isDeleted = message.DeletedAt is not null;

        return new MessageDto(
            message.Id,
            message.ConversationId,
            message.SenderId,
            message.Type.ToString(),
            message.Content,
            isDeleted
                ? []
                : message.Attachments
                    .OrderBy(a => a.SortOrder)
                    .Select(a => new MessageAttachmentDto(
                        a.MediaId,
                        a.Media.Url,
                        a.Media.ThumbnailUrl,
                        a.Media.Width,
                        a.Media.Height))
                    .ToList(),
            message.ClientMessageId,
            message.CreatedAt,
            isDeleted ? null : replyTo,
            isDeleted ? null : forwardedFrom,
            message.EditedAt,
            message.DeletedAt,
            isDeleted ? [] : reactions);
    }

    /// <summary>Ảnh chụp trích dẫn của một tin (dùng cho reply/forward).</summary>
    public static MessageQuoteDto ToQuoteDto(Message message)
    {
        var isDeleted = message.DeletedAt is not null;

        return new MessageQuoteDto(
            message.Id,
            message.SenderId,
            DisplayName(message.Sender),
            isDeleted ? null : QuotePreview(message.Content),
            message.Type.ToString(),
            isDeleted);
    }

    /// <summary>Nguồn của tin chuyển tiếp (chỉ cần tên người gửi gốc).</summary>
    public static ForwardedFromDto ToForwardedFromDto(Message message) =>
        new(message.Id, message.SenderId, DisplayName(message.Sender));

    /// <summary>Tên hiển thị của người dùng, có phương án dự phòng là tên đăng nhập.</summary>
    public static string DisplayName(ApplicationUser? user)
    {
        if (user is null)
        {
            return string.Empty;
        }

        var displayName = user.Profile?.DisplayName;
        return string.IsNullOrWhiteSpace(displayName) ? user.UserName ?? string.Empty : displayName;
    }

    /// <summary>
    /// Xem trước nội dung tin. Tin chỉ có ảnh trả chuỗi rỗng — client tự hiển thị nhãn "[Ảnh]"
    /// theo `type` để giữ đa ngôn ngữ ở phía client.
    /// </summary>
    public static string Preview(string? content, int maxLength = PreviewLength)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content.Trim().ReplaceLineEndings(" ");
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    /// <summary>Xem trước ngắn hơn dùng trong trích dẫn phản hồi.</summary>
    public static string QuotePreview(string? content) => Preview(content, QuotePreviewLength);
}
