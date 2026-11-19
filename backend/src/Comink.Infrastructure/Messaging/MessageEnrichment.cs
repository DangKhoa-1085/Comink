// Nạp dữ liệu phụ cho DTO tin nhắn (trích dẫn, nguồn chuyển tiếp, cảm xúc) theo LÔ,
// tránh N+1 khi trả một trang lịch sử (NC Giai đoạn 7 — Ưu tiên 2).
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Messaging;

internal static class MessageEnrichment
{
    /// <summary>
    /// Chuyển danh sách entity (đã Include <c>Attachments.Media</c>) sang DTO đầy đủ.
    /// Dùng 2 truy vấn phụ cho cả trang: một cho tin được trích dẫn/chuyển tiếp, một cho cảm xúc.
    /// </summary>
    public static async Task<IReadOnlyList<MessageDto>> EnrichAsync(
        ApplicationDbContext db,
        IReadOnlyList<Message> messages,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var messageIds = messages.Select(m => m.Id).ToList();

        // Tin được trích dẫn (reply) hoặc là nguồn chuyển tiếp — nạp một lượt kèm tên người gửi.
        var referencedIds = messages
            .SelectMany(m => new[] { m.ReplyToMessageId, m.ForwardedFromMessageId })
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var referenced = new Dictionary<Guid, Message>();
        if (referencedIds.Count > 0)
        {
            var referencedMessages = await db.Messages
                .AsNoTracking()
                .Include(m => m.Sender)
                .ThenInclude(u => u.Profile)
                .Where(m => referencedIds.Contains(m.Id))
                .ToListAsync(cancellationToken);

            referenced = referencedMessages.ToDictionary(m => m.Id);
        }

        // Cảm xúc của cả trang trong một truy vấn.
        var reactionRows = await db.MessageReactions
            .AsNoTracking()
            .Where(r => messageIds.Contains(r.MessageId))
            .Select(r => new { r.MessageId, r.Emoji, r.UserId })
            .ToListAsync(cancellationToken);

        var reactionsByMessage = reactionRows
            .GroupBy(r => r.MessageId)
            .ToDictionary(
                group => group.Key,
                group => BuildReactionSummaries(
                    group.Select(row => (row.Emoji, row.UserId)).ToList(),
                    currentUserId));

        return messages.Select(message =>
        {
            MessageQuoteDto? replyTo = null;
            if (message.ReplyToMessageId is not null &&
                referenced.TryGetValue(message.ReplyToMessageId.Value, out var replyTarget))
            {
                replyTo = MessagingMapping.ToQuoteDto(replyTarget);
            }

            ForwardedFromDto? forwardedFrom = null;
            if (message.ForwardedFromMessageId is not null &&
                referenced.TryGetValue(message.ForwardedFromMessageId.Value, out var origin))
            {
                forwardedFrom = MessagingMapping.ToForwardedFromDto(origin);
            }

            var reactions = reactionsByMessage.GetValueOrDefault(message.Id) ?? [];
            return MessagingMapping.ToMessageDto(message, replyTo, forwardedFrom, reactions);
        }).ToList();
    }

    /// <summary>Một DTO tin nhắn đầy đủ theo id (dùng cho phản hồi của thao tác lẻ).</summary>
    public static async Task<MessageDto?> LoadAsync(
        ApplicationDbContext db,
        Guid messageId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var message = await db.Messages
            .AsNoTracking()
            .Include(m => m.Attachments)
            .ThenInclude(a => a.Media)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message is null)
        {
            return null;
        }

        var enriched = await EnrichAsync(db, [message], currentUserId, cancellationToken);
        return enriched.Single();
    }

    /// <summary>Gộp cảm xúc theo emoji, sắp theo thứ tự danh mục rồi tới emoji lạ (dữ liệu cũ).</summary>
    private static IReadOnlyList<ReactionSummaryDto> BuildReactionSummaries(
        IReadOnlyList<(string Emoji, Guid UserId)> rows,
        Guid currentUserId)
    {
        var order = ReactionCatalog.All
            .Select((definition, index) => (definition.Emoji, index))
            .ToDictionary(item => item.Emoji, item => item.index);

        return rows
            .GroupBy(row => row.Emoji)
            .Select(group =>
            {
                var key = ReactionCatalog.All
                    .FirstOrDefault(definition => definition.Emoji == group.Key)?.Key ?? group.Key;

                return new ReactionSummaryDto(
                    key,
                    group.Key,
                    group.Count(),
                    group.Any(row => row.UserId == currentUserId));
            })
            .OrderBy(reaction => order.GetValueOrDefault(reaction.Emoji, int.MaxValue))
            .ThenBy(reaction => reaction.Emoji, StringComparer.Ordinal)
            .ToList();
    }
}
