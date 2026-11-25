// Nạp danh sách tin ghim của một hội thoại (NC Giai đoạn 7 — Ưu tiên 2).
using Comink.Application.Messaging.Dtos;
using Comink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Messaging;

internal static class PinnedMessagesLoader
{
    /// <summary>
    /// Danh sách ghim hiện tại (tối đa 10), mới ghim trước. Dùng chung cho endpoint đọc,
    /// sự kiện real-time và bước dọn ghim khi thu hồi tin.
    /// </summary>
    public static async Task<PinnedMessagesDto> LoadAsync(
        ApplicationDbContext db,
        Guid conversationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.PinnedMessages
            .AsNoTracking()
            .Include(p => p.Message)
            .ThenInclude(m => m.Attachments)
            .ThenInclude(a => a.Media)
            .Include(p => p.PinnedByUser)
            .ThenInclude(u => u.Profile)
            .Where(p => p.ConversationId == conversationId)
            .OrderByDescending(p => p.PinnedAt)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new PinnedMessagesDto(conversationId, []);
        }

        var messages = rows.Select(row => row.Message).ToList();
        var messageDtos = await MessageEnrichment.EnrichAsync(db, messages, currentUserId, cancellationToken);
        var byId = messageDtos.ToDictionary(dto => dto.Id);

        var pinned = rows
            .Where(row => byId.ContainsKey(row.MessageId))
            .Select(row => new PinnedMessageDto(
                byId[row.MessageId],
                row.PinnedBy,
                MessagingMapping.DisplayName(row.PinnedByUser),
                row.PinnedAt))
            .ToList();

        return new PinnedMessagesDto(conversationId, pinned);
    }
}
