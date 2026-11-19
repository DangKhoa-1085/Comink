// Nghiệp vụ tin nhắn: gửi (idempotent), đọc lịch sử theo con trỏ, phản hồi, chỉnh sửa,
// thu hồi và thả cảm xúc (docs/05_api_contract.md mục 8.5–8.6, 8.12–8.15).
using Comink.Application.Common;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Comink.Application.Notifications.Abstractions;
using Comink.Domain.Enums;
using Comink.Domain.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Messaging;

/// <summary>Gửi và thao tác trên tin nhắn trong một hội thoại.</summary>
public class MessageService : IMessageService
{
    private readonly ApplicationDbContext _db;
    private readonly IChatNotifier _notifier;
    private readonly INotificationService? _notifications;

    public MessageService(
        ApplicationDbContext db,
        IChatNotifier notifier,
        INotificationService? notifications = null)
    {
        _db = db;
        _notifier = notifier;
        _notifications = notifications;
    }

    /// <inheritdoc />
    public async Task<OperationResult<MessageSendResult>> SendAsync(
        Guid currentUserId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(currentUserId, conversationId, cancellationToken))
        {
            return OperationResult<MessageSendResult>.Failure(ErrorCodes.ConversationNotFound);
        }

        // 1) Đã gửi trước đó với cùng client_message_id → trả lại tin cũ (idempotent).
        var existingId = await _db.Messages
            .AsNoTracking()
            .Where(m => m.SenderId == currentUserId && m.ClientMessageId == request.ClientMessageId)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingId is not null)
        {
            var existingMessage = await MessageEnrichment.LoadAsync(_db, existingId.Value, currentUserId, cancellationToken);
            return existingMessage is null
                ? OperationResult<MessageSendResult>.Failure(ErrorCodes.InternalError)
                : OperationResult<MessageSendResult>.Success(new MessageSendResult(existingMessage, false));
        }

        var mediaIds = request.MediaIds?.Distinct().ToList() ?? [];

        // 2) Kiểm tra nội dung: phải có chữ hoặc ảnh.
        if (!MessageRules.HasPayload(request.Content, mediaIds.Count))
        {
            return OperationResult<MessageSendResult>.Failure(ErrorCodes.MessageContentRequired);
        }

        if (mediaIds.Count > MessageRules.MaxAttachments)
        {
            return OperationResult<MessageSendResult>.Failure(ErrorCodes.MessageTooManyAttachments);
        }

        if (!MessageRules.IsContentLengthValid(request.Content))
        {
            return OperationResult<MessageSendResult>.Failure(ErrorCodes.ValidationFailed);
        }

        // 3) Tin được phản hồi phải thuộc ĐÚNG hội thoại này và còn hiệu lực (NC Ưu tiên 2).
        if (request.ReplyToMessageId is not null)
        {
            var replyTarget = await _db.Messages
                .AsNoTracking()
                .Where(m => m.Id == request.ReplyToMessageId.Value)
                .Select(m => new { m.ConversationId, m.DeletedAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (replyTarget is null || replyTarget.ConversationId != conversationId)
            {
                return OperationResult<MessageSendResult>.Failure(ErrorCodes.MessageNotFound);
            }

            if (replyTarget.DeletedAt is not null)
            {
                return OperationResult<MessageSendResult>.Failure(ErrorCodes.MessageAlreadyDeleted);
            }
        }

        // 4) Ảnh phải do chính người gửi tải lên với context = Message.
        if (mediaIds.Count > 0)
        {
            var ownedCount = await _db.MediaAssets
                .AsNoTracking()
                .CountAsync(
                    m => mediaIds.Contains(m.Id) &&
                         m.OwnerId == currentUserId &&
                         m.Context == MediaContext.Message &&
                         m.DeletedAt == null,
                    cancellationToken);

            if (ownedCount != mediaIds.Count)
            {
                return OperationResult<MessageSendResult>.Failure(ErrorCodes.MediaNotOwned);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = currentUserId,
            Type = MessageRules.ResolveType(mediaIds.Count),
            Content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim(),
            ClientMessageId = request.ClientMessageId,
            ReplyToMessageId = request.ReplyToMessageId,
            CreatedAt = now
        };

        for (var index = 0; index < mediaIds.Count; index++)
        {
            message.Attachments.Add(new MessageAttachment
            {
                MessageId = message.Id,
                MediaId = mediaIds[index],
                SortOrder = (short)index
            });
        }

        _db.Messages.Add(message);

        // 5) Cập nhật con trỏ tin cuối của hội thoại trong cùng transaction.
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            return OperationResult<MessageSendResult>.Failure(ErrorCodes.ConversationNotFound);
        }

        conversation.LastMessageId = message.Id;
        conversation.LastMessageAt = now;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Hai lần gửi đồng thời cùng client_message_id → bên thua trả tin đã lưu.
            _db.ChangeTracker.Clear();
            var concurrentId = await _db.Messages
                .AsNoTracking()
                .Where(m => m.SenderId == currentUserId && m.ClientMessageId == request.ClientMessageId)
                .Select(m => (Guid?)m.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (concurrentId is null)
            {
                return OperationResult<MessageSendResult>.Failure(ErrorCodes.InternalError);
            }

            var concurrentMessage = await MessageEnrichment.LoadAsync(
                _db, concurrentId.Value, currentUserId, cancellationToken);
            return concurrentMessage is null
                ? OperationResult<MessageSendResult>.Failure(ErrorCodes.InternalError)
                : OperationResult<MessageSendResult>.Success(new MessageSendResult(concurrentMessage, false));
        }

        var dto = await MessageEnrichment.LoadAsync(_db, message.Id, currentUserId, cancellationToken);
        if (dto is null)
        {
            return OperationResult<MessageSendResult>.Failure(ErrorCodes.InternalError);
        }

        // 6) Phát sự kiện thời gian thực tới từng thành viên của hội thoại.
        var memberUserIds = await GetMemberUserIdsAsync(conversationId, cancellationToken);
        await _notifier.MessageCreatedAsync(conversationId, memberUserIds, dto, cancellationToken);

        // Push cho người nhận khi họ không mở app; không tạo dòng notifications (docs/03 mục 3.7).
        if (_notifications is not null)
        {
            var recipientIds = memberUserIds.Where(id => id != currentUserId).ToList();
            if (recipientIds.Count > 0)
            {
                var senderName = await _db.UserProfiles
                    .AsNoTracking()
                    .Where(p => p.UserId == currentUserId)
                    .Select(p => p.DisplayName)
                    .FirstOrDefaultAsync(cancellationToken) ?? "Comink";
                var preview = string.IsNullOrWhiteSpace(dto.Content)
                    ? (dto.Attachments.Count > 0 ? "Ảnh" : "Tin nhắn mới")
                    : dto.Content!;
                await _notifications.PushToUsersAsync(
                    recipientIds,
                    "Tin nhắn mới",
                    $"{senderName}: {preview}",
                    new Dictionary<string, string>
                    {
                        ["type"] = "Message",
                        ["conversationId"] = conversationId.ToString(),
                        ["messageId"] = message.Id.ToString(),
                        ["senderId"] = currentUserId.ToString(),
                    },
                    highPriority: true,
                    cancellationToken);
            }
        }

        return OperationResult<MessageSendResult>.Success(new MessageSendResult(dto, true));
    }

    /// <inheritdoc />
    public async Task<OperationResult<MessageDto>> EditAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        EditMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(currentUserId, conversationId, cancellationToken))
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        var message = await _db.Messages
            .FirstOrDefaultAsync(
                m => m.Id == messageId && m.ConversationId == conversationId,
                cancellationToken);

        if (message is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.MessageNotFound);
        }

        var now = DateTimeOffset.UtcNow;
        var outcome = MessageActionRules.EvaluateEdit(
            message.SenderId,
            currentUserId,
            message.DeletedAt is not null,
            message.Type,
            message.CreatedAt,
            now);

        if (outcome != MessageActionOutcome.Allowed)
        {
            return OperationResult<MessageDto>.Failure(outcome switch
            {
                MessageActionOutcome.NotOwner => ErrorCodes.MessageNotOwned,
                MessageActionOutcome.AlreadyDeleted => ErrorCodes.MessageAlreadyDeleted,
                MessageActionOutcome.NotEditable => ErrorCodes.MessageNotEditable,
                _ => ErrorCodes.MessageEditWindowExpired
            });
        }

        var content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim();
        var attachmentCount = await _db.MessageAttachments
            .AsNoTracking()
            .CountAsync(a => a.MessageId == messageId, cancellationToken);

        if (!MessageRules.HasPayload(content, attachmentCount))
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.MessageContentRequired);
        }

        if (!MessageRules.IsContentLengthValid(content))
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.ValidationFailed);
        }

        message.Content = content;
        message.EditedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await LoadEnrichedOrFailAsync(messageId, currentUserId, cancellationToken);
        if (dto is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.InternalError);
        }

        await BroadcastUpdatedAsync(conversationId, dto, cancellationToken);
        return OperationResult<MessageDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<OperationResult<MessageDto>> RecallAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(currentUserId, conversationId, cancellationToken))
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        var message = await _db.Messages
            .FirstOrDefaultAsync(
                m => m.Id == messageId && m.ConversationId == conversationId,
                cancellationToken);

        if (message is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.MessageNotFound);
        }

        var now = DateTimeOffset.UtcNow;
        var outcome = MessageActionRules.EvaluateRecall(
            message.SenderId,
            currentUserId,
            message.DeletedAt is not null,
            message.Type,
            message.CreatedAt,
            now);

        // Thu hồi lặp lại là vô hại: trả trạng thái hiện tại, không phát lại sự kiện.
        if (outcome == MessageActionOutcome.AlreadyDeleted)
        {
            var current = await LoadEnrichedOrFailAsync(messageId, currentUserId, cancellationToken);
            return current is null
                ? OperationResult<MessageDto>.Failure(ErrorCodes.InternalError)
                : OperationResult<MessageDto>.Success(current);
        }

        if (outcome != MessageActionOutcome.Allowed)
        {
            return OperationResult<MessageDto>.Failure(outcome switch
            {
                MessageActionOutcome.NotOwner => ErrorCodes.MessageNotOwned,
                MessageActionOutcome.NotEditable => ErrorCodes.MessageNotEditable,
                _ => ErrorCodes.MessageRecallWindowExpired
            });
        }

        // Thu hồi mềm: xoá nội dung, giữ dòng để khung "đã thu hồi" đúng chỗ (docs/03 mục 3.3).
        message.Content = null;
        message.EditedAt = null;
        message.DeletedAt = now;

        // Cảm xúc và ghim của tin đã thu hồi không còn ý nghĩa.
        var reactions = await _db.MessageReactions
            .Where(r => r.MessageId == messageId)
            .ToListAsync(cancellationToken);
        if (reactions.Count > 0)
        {
            _db.MessageReactions.RemoveRange(reactions);
        }

        var pin = await _db.PinnedMessages
            .FirstOrDefaultAsync(
                p => p.ConversationId == conversationId && p.MessageId == messageId,
                cancellationToken);
        var pinRemoved = pin is not null;
        if (pin is not null)
        {
            _db.PinnedMessages.Remove(pin);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dto = await LoadEnrichedOrFailAsync(messageId, currentUserId, cancellationToken);
        if (dto is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.InternalError);
        }

        await BroadcastUpdatedAsync(conversationId, dto, cancellationToken);

        if (pinRemoved)
        {
            await BroadcastPinnedAsync(conversationId, currentUserId, cancellationToken);
        }

        return OperationResult<MessageDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<OperationResult<MessageDto>> AddReactionAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        string reaction,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(currentUserId, conversationId, cancellationToken))
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        var target = await _db.Messages
            .AsNoTracking()
            .Where(m => m.Id == messageId && m.ConversationId == conversationId)
            .Select(m => new { m.Id, m.DeletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.MessageNotFound);
        }

        if (target.DeletedAt is not null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.MessageAlreadyDeleted);
        }

        var emoji = ReactionCatalog.ToEmoji(reaction);
        if (emoji is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.MessageReactionInvalid);
        }

        var exists = await _db.MessageReactions
            .AnyAsync(
                r => r.MessageId == messageId && r.UserId == currentUserId && r.Emoji == emoji,
                cancellationToken);

        if (!exists)
        {
            _db.MessageReactions.Add(new MessageReaction
            {
                MessageId = messageId,
                UserId = currentUserId,
                Emoji = emoji,
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        var dto = await LoadEnrichedOrFailAsync(messageId, currentUserId, cancellationToken);
        if (dto is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.InternalError);
        }

        if (!exists)
        {
            await BroadcastUpdatedAsync(conversationId, dto, cancellationToken);
        }

        return OperationResult<MessageDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<OperationResult<MessageDto>> RemoveReactionAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        string reaction,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(currentUserId, conversationId, cancellationToken))
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        var target = await _db.Messages
            .AsNoTracking()
            .Where(m => m.Id == messageId && m.ConversationId == conversationId)
            .Select(m => new { m.Id, m.DeletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.MessageNotFound);
        }

        if (target.DeletedAt is not null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.MessageAlreadyDeleted);
        }

        var emoji = ReactionCatalog.ToEmoji(reaction);
        if (emoji is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.MessageReactionInvalid);
        }

        var existing = await _db.MessageReactions
            .FirstOrDefaultAsync(
                r => r.MessageId == messageId && r.UserId == currentUserId && r.Emoji == emoji,
                cancellationToken);

        if (existing is not null)
        {
            _db.MessageReactions.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var dto = await LoadEnrichedOrFailAsync(messageId, currentUserId, cancellationToken);
        if (dto is null)
        {
            return OperationResult<MessageDto>.Failure(ErrorCodes.InternalError);
        }

        if (existing is not null)
        {
            await BroadcastUpdatedAsync(conversationId, dto, cancellationToken);
        }

        return OperationResult<MessageDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<MessageDto>>> GetHistoryAsync(
        Guid currentUserId,
        Guid conversationId,
        string? before,
        string? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(currentUserId, conversationId, cancellationToken))
        {
            return OperationResult<PagedList<MessageDto>>.Failure(ErrorCodes.ConversationNotFound);
        }

        if (before is not null && after is not null)
        {
            return OperationResult<PagedList<MessageDto>>.Failure(ErrorCodes.ValidationFailed);
        }

        var pageSize = Math.Clamp(limit, 1, 50);

        // Đồng bộ sau khi mất kết nối: lấy tin MỚI HƠN con trỏ, cũ nhất trước.
        if (after is not null)
        {
            var afterCursor = CursorCodec.TryDecode(after);
            if (afterCursor is null)
            {
                return OperationResult<PagedList<MessageDto>>.Failure(ErrorCodes.MessageInvalidCursor);
            }

            var afterCreatedAt = afterCursor.Value.CreatedAt;
            var afterId = afterCursor.Value.Id;

            var newRows = await _db.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .Where(m => m.CreatedAt > afterCreatedAt ||
                            (m.CreatedAt == afterCreatedAt && m.Id.CompareTo(afterId) > 0))
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .Take(pageSize + 1)
                .Include(m => m.Attachments)
                .ThenInclude(a => a.Media)
                .ToListAsync(cancellationToken);

            var hasMoreNew = newRows.Count > pageSize;
            var newPage = hasMoreNew ? newRows.Take(pageSize).ToList() : newRows;
            var newItems = await MessageEnrichment.EnrichAsync(_db, newPage, currentUserId, cancellationToken);
            var nextNewCursor = hasMoreNew && newPage.Count > 0
                ? CursorCodec.Encode(newPage[^1].CreatedAt, newPage[^1].Id)
                : null;

            return OperationResult<PagedList<MessageDto>>.Success(
                new PagedList<MessageDto>(newItems, nextNewCursor, hasMoreNew));
        }

        var query = _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (before is not null)
        {
            var beforeCursor = CursorCodec.TryDecode(before);
            if (beforeCursor is null)
            {
                return OperationResult<PagedList<MessageDto>>.Failure(ErrorCodes.MessageInvalidCursor);
            }

            var beforeCreatedAt = beforeCursor.Value.CreatedAt;
            var beforeId = beforeCursor.Value.Id;
            query = query.Where(m => m.CreatedAt < beforeCreatedAt ||
                                     (m.CreatedAt == beforeCreatedAt && m.Id.CompareTo(beforeId) < 0));
        }

        var rows = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(pageSize + 1)
            .Include(m => m.Attachments)
            .ThenInclude(a => a.Media)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var page = hasMore ? rows.Take(pageSize).ToList() : rows;
        var items = await MessageEnrichment.EnrichAsync(_db, page, currentUserId, cancellationToken);
        var nextCursor = hasMore && page.Count > 0
            ? CursorCodec.Encode(page[^1].CreatedAt, page[^1].Id)
            : null;

        return OperationResult<PagedList<MessageDto>>.Success(
            new PagedList<MessageDto>(items, nextCursor, hasMore));
    }

    private Task<bool> IsMemberAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken) =>
        _db.ConversationMembers
            .AsNoTracking()
            .AnyAsync(
                m => m.ConversationId == conversationId && m.UserId == currentUserId && m.LeftAt == null,
                cancellationToken);

    private Task<List<Guid>> GetMemberUserIdsAsync(Guid conversationId, CancellationToken cancellationToken) =>
        _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

    private Task<MessageDto?> LoadEnrichedOrFailAsync(
        Guid messageId,
        Guid currentUserId,
        CancellationToken cancellationToken) =>
        MessageEnrichment.LoadAsync(_db, messageId, currentUserId, cancellationToken);

    private async Task BroadcastUpdatedAsync(
        Guid conversationId,
        MessageDto message,
        CancellationToken cancellationToken)
    {
        var memberUserIds = await GetMemberUserIdsAsync(conversationId, cancellationToken);
        await _notifier.MessageUpdatedAsync(conversationId, memberUserIds, message, cancellationToken);
    }

    private async Task BroadcastPinnedAsync(
        Guid conversationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var memberUserIds = await GetMemberUserIdsAsync(conversationId, cancellationToken);
        var pinned = await PinnedMessagesLoader.LoadAsync(_db, conversationId, currentUserId, cancellationToken);
        await _notifier.PinnedMessagesChangedAsync(conversationId, memberUserIds, pinned, cancellationToken);
    }

    /// <summary>Lỗi vi phạm ràng buộc duy nhất của PostgreSQL (SqlState 23505).</summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
