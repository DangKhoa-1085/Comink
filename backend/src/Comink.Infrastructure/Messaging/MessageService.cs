// Nghiệp vụ tin nhắn: gửi (idempotent) và đọc lịch sử theo con trỏ (docs/05_api_contract.md mục 8.5–8.6).
using Comink.Application.Common;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Enums;
using Comink.Domain.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Messaging;

/// <summary>Gửi và đọc tin nhắn trong một hội thoại.</summary>
public class MessageService : IMessageService
{
    private readonly ApplicationDbContext _db;
    private readonly IChatNotifier _notifier;

    public MessageService(ApplicationDbContext db, IChatNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
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
            var existingMessage = await LoadMessageAsync(existingId.Value, cancellationToken);
            return existingMessage is null
                ? OperationResult<MessageSendResult>.Failure(ErrorCodes.InternalError)
                : OperationResult<MessageSendResult>.Success(
                    new MessageSendResult(MessagingMapping.ToMessageDto(existingMessage), false));
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

        // 3) Ảnh phải do chính người gửi tải lên với context = Message.
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

        // 4) Cập nhật con trỏ tin cuối của hội thoại trong cùng transaction.
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

            var concurrentMessage = await LoadMessageAsync(concurrentId.Value, cancellationToken);
            return concurrentMessage is null
                ? OperationResult<MessageSendResult>.Failure(ErrorCodes.InternalError)
                : OperationResult<MessageSendResult>.Success(
                    new MessageSendResult(MessagingMapping.ToMessageDto(concurrentMessage), false));
        }

        var saved = await LoadMessageAsync(message.Id, cancellationToken);
        if (saved is null)
        {
            return OperationResult<MessageSendResult>.Failure(ErrorCodes.InternalError);
        }

        var dto = MessagingMapping.ToMessageDto(saved);

        // 5) Phát sự kiện thời gian thực tới từng thành viên của hội thoại.
        var memberUserIds = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        await _notifier.MessageCreatedAsync(conversationId, memberUserIds, dto, cancellationToken);

        return OperationResult<MessageSendResult>.Success(new MessageSendResult(dto, true));
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
            var newItems = newPage.Select(MessagingMapping.ToMessageDto).ToList();
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
        var items = page.Select(MessagingMapping.ToMessageDto).ToList();
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

    private Task<Message?> LoadMessageAsync(Guid messageId, CancellationToken cancellationToken) =>
        _db.Messages
            .AsNoTracking()
            .Include(m => m.Attachments)
            .ThenInclude(a => a.Media)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

    /// <summary>Lỗi vi phạm ràng buộc duy nhất của PostgreSQL (SqlState 23505).</summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
