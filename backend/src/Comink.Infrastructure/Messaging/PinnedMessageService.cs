// Nghiệp vụ ghim tin nhắn (NC Giai đoạn 7 — Ưu tiên 2, quyết định D3).
using Comink.Application.Common;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Messaging;

/// <summary>Ghim/bỏ ghim tin; tối đa 10 tin mỗi hội thoại; phát sự kiện cho thành viên.</summary>
public class PinnedMessageService : IPinnedMessageService
{
    private readonly ApplicationDbContext _db;
    private readonly IChatNotifier _notifier;

    public PinnedMessageService(ApplicationDbContext db, IChatNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<OperationResult<PinnedMessagesDto>> GetAsync(
        Guid currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(currentUserId, conversationId, cancellationToken))
        {
            return OperationResult<PinnedMessagesDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        var pinned = await PinnedMessagesLoader.LoadAsync(
            _db, conversationId, currentUserId, cancellationToken);

        return OperationResult<PinnedMessagesDto>.Success(pinned);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PinnedMessagesDto>> PinAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(currentUserId, conversationId, cancellationToken))
        {
            return OperationResult<PinnedMessagesDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        var message = await _db.Messages
            .AsNoTracking()
            .Where(m => m.Id == messageId && m.ConversationId == conversationId)
            .Select(m => new { m.Id, m.DeletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            return OperationResult<PinnedMessagesDto>.Failure(ErrorCodes.MessageNotFound);
        }

        if (message.DeletedAt is not null)
        {
            return OperationResult<PinnedMessagesDto>.Failure(ErrorCodes.MessageAlreadyDeleted);
        }

        var alreadyPinned = await _db.PinnedMessages
            .AsNoTracking()
            .AnyAsync(
                p => p.ConversationId == conversationId && p.MessageId == messageId,
                cancellationToken);

        // Ghim lặp lại là idempotent — không phát lại sự kiện.
        if (alreadyPinned)
        {
            var current = await PinnedMessagesLoader.LoadAsync(
                _db, conversationId, currentUserId, cancellationToken);
            return OperationResult<PinnedMessagesDto>.Success(current);
        }

        var pinnedCount = await _db.PinnedMessages
            .AsNoTracking()
            .CountAsync(p => p.ConversationId == conversationId, cancellationToken);

        if (pinnedCount >= MessageActionRules.MaxPinnedMessages)
        {
            return OperationResult<PinnedMessagesDto>.Failure(ErrorCodes.MessagePinLimitReached);
        }

        _db.PinnedMessages.Add(new PinnedMessage
        {
            ConversationId = conversationId,
            MessageId = messageId,
            PinnedBy = currentUserId,
            PinnedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await BroadcastAsync(conversationId, currentUserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PinnedMessagesDto>> UnpinAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(currentUserId, conversationId, cancellationToken))
        {
            return OperationResult<PinnedMessagesDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        var pin = await _db.PinnedMessages
            .FirstOrDefaultAsync(
                p => p.ConversationId == conversationId && p.MessageId == messageId,
                cancellationToken);

        // Bỏ ghim tin không được ghim: idempotent, không phát sự kiện.
        if (pin is null)
        {
            var current = await PinnedMessagesLoader.LoadAsync(
                _db, conversationId, currentUserId, cancellationToken);
            return OperationResult<PinnedMessagesDto>.Success(current);
        }

        _db.PinnedMessages.Remove(pin);
        await _db.SaveChangesAsync(cancellationToken);

        return await BroadcastAsync(conversationId, currentUserId, cancellationToken);
    }

    private Task<bool> IsMemberAsync(Guid currentUserId, Guid conversationId, CancellationToken cancellationToken) =>
        _db.ConversationMembers
            .AsNoTracking()
            .AnyAsync(
                m => m.ConversationId == conversationId && m.UserId == currentUserId && m.LeftAt == null,
                cancellationToken);

    private async Task<OperationResult<PinnedMessagesDto>> BroadcastAsync(
        Guid conversationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var pinned = await PinnedMessagesLoader.LoadAsync(
            _db, conversationId, currentUserId, cancellationToken);

        var memberUserIds = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        await _notifier.PinnedMessagesChangedAsync(
            conversationId, memberUserIds, pinned, cancellationToken);

        return OperationResult<PinnedMessagesDto>.Success(pinned);
    }
}
