// Nghiệp vụ hội thoại (docs/05_api_contract.md mục 8.2–8.4).
using Comink.Application.Common;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Enums;
using Comink.Domain.Messaging;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Comink.Infrastructure.Social;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Messaging;

/// <summary>
/// Mở và đọc hội thoại. Giai đoạn 3 chỉ có chat riêng 1–1, mỗi cặp người đúng một hội thoại
/// (khoá <c>direct_key</c> + chỉ mục duy nhất có điều kiện).
/// </summary>
public class ConversationService : IConversationService
{
    private readonly ApplicationDbContext _db;
    private readonly IPresenceService _presence;
    private readonly IChatNotifier _notifier;

    public ConversationService(
        ApplicationDbContext db,
        IPresenceService presence,
        IChatNotifier notifier)
    {
        _db = db;
        _presence = presence;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<OperationResult<ConversationOpenResult>> OpenDirectAsync(
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (ConversationRules.IsSelfConversation(currentUserId, targetUserId))
        {
            return OperationResult<ConversationOpenResult>.Failure(ErrorCodes.MessagingCannotMessageSelf);
        }

        // Không mở được hội thoại với tài khoản Admin (trừ khi người gửi là Admin) — 404 để ẩn.
        if (!await AdminAccounts.IsActiveAndInteractableAsync(_db, currentUserId, targetUserId, cancellationToken))
        {
            return OperationResult<ConversationOpenResult>.Failure(ErrorCodes.NotFound);
        }

        // Quyết định D1 (docs/tasks/phase3_messaging.md mục 2): chỉ bạn bè mới nhắn tin được.
        if (!await SocialLookup.AreFriendsAsync(_db, currentUserId, targetUserId, cancellationToken))
        {
            return OperationResult<ConversationOpenResult>.Failure(ErrorCodes.MessagingNotFriends);
        }

        var directKey = ConversationRules.BuildDirectKey(currentUserId, targetUserId);
        var existingId = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.Type == ConversationType.Direct && c.DirectKey == directKey)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingId is not null)
        {
            // Đã có hội thoại → trả lại với WasCreated = false để API trả 200 (không phải 201).
            return await WrapAsync(currentUserId, existingId.Value, false, cancellationToken);
        }

        var conversation = new Conversation
        {
            Type = ConversationType.Direct,
            DirectKey = directKey,
            CreatedBy = currentUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        conversation.Members.Add(new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = currentUserId,
            Role = MemberRole.Member,
            JoinedAt = conversation.CreatedAt
        });
        conversation.Members.Add(new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = targetUserId,
            Role = MemberRole.Member,
            JoinedAt = conversation.CreatedAt
        });

        _db.Conversations.Add(conversation);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Hai client bấm "Nhắn tin" cùng lúc → bên thua lấy hội thoại vừa được tạo.
            _db.ChangeTracker.Clear();
            var concurrentId = await _db.Conversations
                .AsNoTracking()
                .Where(c => c.Type == ConversationType.Direct && c.DirectKey == directKey)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return concurrentId is null
                ? OperationResult<ConversationOpenResult>.Failure(ErrorCodes.InternalError)
                : await WrapAsync(currentUserId, concurrentId.Value, false, cancellationToken);
        }

        return await WrapAsync(currentUserId, conversation.Id, true, cancellationToken);
    }

    /// <summary>Đọc lại hội thoại vừa tạo/tìm thấy và gắn cờ <paramref name="wasCreated"/>.</summary>
    private async Task<OperationResult<ConversationOpenResult>> WrapAsync(
        Guid currentUserId,
        Guid conversationId,
        bool wasCreated,
        CancellationToken cancellationToken)
    {
        var inner = await GetByIdAsync(currentUserId, conversationId, cancellationToken);

        return inner.Succeeded && inner.Value is not null
            ? OperationResult<ConversationOpenResult>.Success(
                new ConversationOpenResult(inner.Value, wasCreated))
            : OperationResult<ConversationOpenResult>.Failure(
                inner.ErrorCode ?? ErrorCodes.InternalError);
    }

    /// <inheritdoc />
    public async Task<OperationResult<ConversationDto>> CreateGroupAsync(
        Guid currentUserId,
        CreateGroupConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Bỏ chính mình nếu client lỡ gửi kèm; khử trùng lặp.
        var inviteeIds = request.ParticipantIds
            .Where(id => id != currentUserId)
            .Distinct()
            .ToList();

        var memberCount = inviteeIds.Count + 1;
        if (!ConversationRules.IsGroupSizeValid(memberCount))
        {
            return OperationResult<ConversationDto>.Failure(
                memberCount < ConversationRules.MinGroupMembers
                    ? ErrorCodes.MessagingGroupTooFew
                    : ErrorCodes.MessagingGroupTooMany);
        }

        // Chỉ mời được bạn bè còn hoạt động (cùng quy tắc D1 với chat 1–1).
        foreach (var inviteeId in inviteeIds)
        {
            // Không mời được tài khoản Admin vào nhóm (trừ khi người tạo là Admin).
            if (!await AdminAccounts.IsActiveAndInteractableAsync(_db, currentUserId, inviteeId, cancellationToken))
            {
                return OperationResult<ConversationDto>.Failure(ErrorCodes.NotFound);
            }

            if (!await SocialLookup.AreFriendsAsync(_db, currentUserId, inviteeId, cancellationToken))
            {
                return OperationResult<ConversationDto>.Failure(ErrorCodes.MessagingNotFriends);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Type = ConversationType.Group,
            Title = request.Name.Trim(),
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        // Người tạo là Owner; người được mời là Member.
        conversation.Members.Add(new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = currentUserId,
            Role = MemberRole.Owner,
            JoinedAt = now
        });

        foreach (var inviteeId in inviteeIds)
        {
            conversation.Members.Add(new ConversationMember
            {
                ConversationId = conversation.Id,
                UserId = inviteeId,
                Role = MemberRole.Member,
                JoinedAt = now
            });
        }

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken);

        // Trả chi tiết để client có ngay danh sách thành viên.
        return await GetByIdAsync(currentUserId, conversation.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<ConversationSummaryDto>>> GetMyConversationsAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(limit, 1, 50);
        var decoded = CursorCodec.TryDecode(cursor);
        if (cursor is not null && decoded is null)
        {
            return OperationResult<PagedList<ConversationSummaryDto>>.Failure(ErrorCodes.MessageInvalidCursor);
        }

        // Hội thoại chưa có tin dùng created_at làm khoá sắp xếp để không có NULL trong con trỏ.
        // Chỉ liệt kê chat 1–1 và nhóm (quyết định D6: không còn hội thoại nhóm "ẩn").
        var baseQuery = _db.Conversations
            .AsNoTracking()
            .Where(c => c.Members.Any(m => m.UserId == currentUserId && m.LeftAt == null) &&
                        (c.Type == ConversationType.Direct || c.Type == ConversationType.Group));

        if (decoded is not null)
        {
            var cursorKey = decoded.Value.CreatedAt;
            var cursorId = decoded.Value.Id;
            baseQuery = baseQuery.Where(c =>
                (c.LastMessageAt ?? c.CreatedAt) < cursorKey ||
                ((c.LastMessageAt ?? c.CreatedAt) == cursorKey && c.Id.CompareTo(cursorId) < 0));
        }

        var rows = await baseQuery
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(pageSize + 1)
            .Select(c => new
            {
                c.Id,
                c.Type,
                c.Title,
                c.LastMessageId,
                c.LastMessageAt,
                c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageRows = hasMore ? rows.Take(pageSize).ToList() : rows;

        var conversationIds = pageRows.Select(r => r.Id).ToList();

        // Người còn lại trong hội thoại 1–1 (một truy vấn, không N+1).
        var peers = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId) && m.UserId != currentUserId)
            .Select(m => new
            {
                m.ConversationId,
                m.UserId,
                m.User.UserName,
                DisplayName = m.User.Profile == null ? m.User.UserName : m.User.Profile.DisplayName,
                AvatarUrl = m.User.Profile == null ? null : m.User.Profile.AvatarUrl
            })
            .ToListAsync(cancellationToken);

        // Số thành viên còn hoạt động của từng hội thoại (một truy vấn, dùng cho cả 1–1 và nhóm).
        var memberCounts = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId) && m.LeftAt == null)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var lastMessageIds = pageRows
            .Where(r => r.LastMessageId is not null)
            .Select(r => r.LastMessageId!.Value)
            .ToList();

        var lastMessages = await _db.Messages
            .AsNoTracking()
            .Where(m => lastMessageIds.Contains(m.Id))
            .Select(m => new { m.Id, m.SenderId, m.Content, m.Type, m.CreatedAt })
            .ToListAsync(cancellationToken);

        // Trạng thái online của "người còn lại" trong chat 1–1 (một lượt hỏi Redis).
        var presenceByUser = await _presence.GetStatusesAsync(
            peers.Select(p => p.UserId).Distinct().ToList(),
            cancellationToken);

        // Số tin chưa đọc thật: tin của NGƯỜI KHÁC có (created_at, id) lớn hơn mốc đọc của tôi
        // (docs/03_database_design.md mục 3.3). Một truy vấn join cho cả trang, không N+1.
        var unreadRows = await (
                from message in _db.Messages.AsNoTracking()
                join member in _db.ConversationMembers.AsNoTracking()
                    on message.ConversationId equals member.ConversationId
                where member.UserId == currentUserId
                      && conversationIds.Contains(message.ConversationId)
                      && message.SenderId != currentUserId
                      && (member.LastReadAt == null
                          || message.CreatedAt > member.LastReadAt
                          || (message.CreatedAt == member.LastReadAt
                              && member.LastReadMessageId != null
                              && message.Id.CompareTo(member.LastReadMessageId.Value) > 0))
                group message by message.ConversationId
                into grouped
                select new { ConversationId = grouped.Key, Count = grouped.Count() })
            .ToListAsync(cancellationToken);

        var unreadByConversation = unreadRows.ToDictionary(row => row.ConversationId, row => row.Count);

        var items = pageRows.Select(row =>
        {
            // Chỉ hội thoại 1–1 mới có "peer"; nhóm hiển thị theo Title + số thành viên.
            var peer = row.Type == ConversationType.Direct
                ? peers.FirstOrDefault(p => p.ConversationId == row.Id)
                : null;
            var last = lastMessages.FirstOrDefault(m => m.Id == row.LastMessageId);
            var memberCount = memberCounts.FirstOrDefault(x => x.ConversationId == row.Id)?.Count ?? 0;
            var peerPresence = peer is null ? null : presenceByUser.GetValueOrDefault(peer.UserId);

            return new ConversationSummaryDto(
                row.Id,
                row.Type.ToString(),
                row.Title,
                // Nhóm không có "peer": hiển thị theo Title + số thành viên.
                peer is null
                    ? null
                    : new ConversationPeerDto(
                        peer.UserId,
                        peer.UserName ?? string.Empty,
                        peer.DisplayName ?? string.Empty,
                        peer.AvatarUrl,
                        peerPresence?.IsOnline ?? false,
                        peerPresence?.LastSeenAt),
                last is null
                    ? null
                    : new ConversationLastMessageDto(
                        last.Id,
                        last.SenderId,
                        MessagingMapping.Preview(last.Content),
                        last.Type.ToString(),
                        last.CreatedAt),
                unreadByConversation.GetValueOrDefault(row.Id),
                memberCount);
        }).ToList();

        var nextCursor = hasMore && pageRows.Count > 0
            ? CursorCodec.Encode(
                pageRows[^1].LastMessageAt ?? pageRows[^1].CreatedAt,
                pageRows[^1].Id)
            : null;

        return OperationResult<PagedList<ConversationSummaryDto>>.Success(
            new PagedList<ConversationSummaryDto>(items, nextCursor, hasMore));
    }

    /// <inheritdoc />
    public async Task<OperationResult<ConversationDto>> GetByIdAsync(
        Guid currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Members)
            .ThenInclude(m => m.User)
            .ThenInclude(u => u.Profile)
            .Include(c => c.LastMessage)
            .ThenInclude(m => m!.Attachments)
            .ThenInclude(a => a.Media)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        // Người ngoài nhận 404 thay vì 403 để không lộ sự tồn tại của hội thoại.
        if (conversation is null ||
            conversation.Members.All(m => m.UserId != currentUserId || m.LeftAt is not null))
        {
            return OperationResult<ConversationDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        // Trạng thái online của các thành viên (một lượt hỏi Redis) + mốc đọc của từng người
        // để client hiển thị "Đã đọc"/"Đã xem bởi N" (NC Giai đoạn 7).
        var presenceByUser = await _presence.GetStatusesAsync(
            conversation.Members.Where(m => m.LeftAt is null).Select(m => m.UserId).ToList(),
            cancellationToken);

        var members = conversation.Members
            .OrderBy(m => m.JoinedAt)
            .Select(m =>
            {
                var presence = presenceByUser.GetValueOrDefault(m.UserId);
                return new ConversationMemberDto(
                    m.UserId,
                    m.User.UserName ?? string.Empty,
                    m.User.Profile?.DisplayName ?? m.User.UserName ?? string.Empty,
                    m.User.Profile?.AvatarUrl,
                    presence?.IsOnline ?? false,
                    presence?.LastSeenAt,
                    m.LastReadMessageId,
                    m.LastReadAt);
            })
            .ToList();

        return OperationResult<ConversationDto>.Success(new ConversationDto(
            conversation.Id,
            conversation.Type.ToString(),
            conversation.Title,
            conversation.AvatarUrl,
            conversation.CreatedAt,
            conversation.LastMessageAt,
            members,
            conversation.LastMessage is null ? null : MessagingMapping.ToMessageDto(conversation.LastMessage)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetActiveConversationIdsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default) =>
        await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.UserId == currentUserId && m.LeftAt == null)
            .Select(m => m.ConversationId)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsMemberAsync(
        Guid currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default) =>
        _db.ConversationMembers
            .AsNoTracking()
            .AnyAsync(
                m => m.ConversationId == conversationId && m.UserId == currentUserId && m.LeftAt == null,
                cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetCoMemberUserIdsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default) =>
        await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.UserId != currentUserId &&
                        m.LeftAt == null &&
                        _db.ConversationMembers.Any(mine =>
                            mine.ConversationId == m.ConversationId &&
                            mine.UserId == currentUserId &&
                            mine.LeftAt == null))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<OperationResult<ReadReceiptDto>> MarkReadAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var member = await _db.ConversationMembers
            .FirstOrDefaultAsync(
                m => m.ConversationId == conversationId && m.UserId == currentUserId && m.LeftAt == null,
                cancellationToken);

        if (member is null)
        {
            return OperationResult<ReadReceiptDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        // Tin phải thuộc ĐÚNG hội thoại này — không tin id client gửi (AGENTS.md mục 9.2).
        var message = await _db.Messages
            .AsNoTracking()
            .Where(m => m.Id == messageId && m.ConversationId == conversationId)
            .Select(m => new { m.Id, m.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            return OperationResult<ReadReceiptDto>.Failure(ErrorCodes.MessageNotFound);
        }

        var now = DateTimeOffset.UtcNow;

        // Mốc chỉ tiến: mở lại khung chat hoặc tab cũ gửi mốc cũ không phát lại sự kiện.
        if (!ConversationRules.CanAdvanceReadMarker(
                member.LastReadAt,
                member.LastReadMessageId,
                message.CreatedAt,
                message.Id))
        {
            return OperationResult<ReadReceiptDto>.Success(
                new ReadReceiptDto(
                    conversationId,
                    currentUserId,
                    member.LastReadMessageId ?? message.Id,
                    member.LastReadAt ?? now));
        }

        // Ghi mốc bằng MỘT câu lệnh có điều kiện tiến — hai tab gửi đồng thời cũng không làm
        // mốc bị lùi (điều kiện so sánh nằm ngay trong WHERE).
        var updated = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE conversation_members
             SET last_read_message_id = {message.Id}, last_read_at = {now}
             WHERE conversation_id = {conversationId}
               AND user_id = {currentUserId}
               AND (last_read_at IS NULL
                    OR last_read_at < {message.CreatedAt}
                    OR (last_read_at = {message.CreatedAt} AND last_read_message_id < {message.Id}))
             """,
            cancellationToken);

        if (updated == 0)
        {
            // Tab khác đã ghi mốc mới hơn trong lúc này → trả lại mốc hiện tại, không phát trùng.
            var current = await _db.ConversationMembers
                .AsNoTracking()
                .FirstAsync(
                    m => m.ConversationId == conversationId && m.UserId == currentUserId,
                    cancellationToken);

            return OperationResult<ReadReceiptDto>.Success(
                new ReadReceiptDto(
                    conversationId,
                    currentUserId,
                    current.LastReadMessageId ?? message.Id,
                    current.LastReadAt ?? now));
        }

        var receipt = new ReadReceiptDto(conversationId, currentUserId, message.Id, now);

        // Câu lệnh trên cập nhật thẳng DB (không qua EF) → nạp lại entity đang bị track để
        // trong cùng request các chỗ đọc sau đó thấy đúng mốc mới.
        await _db.Entry(member).ReloadAsync(cancellationToken);

        // Phát cho MỌI thành viên còn hiệu lực để người gửi thấy "Đã đọc" ngay, kể cả khi
        // hội thoại được tạo sau lúc client kết nối (bài học GĐ 3: phát qua user:{id}).
        var memberUserIds = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        await _notifier.MessageReadAsync(conversationId, memberUserIds, receipt, cancellationToken);

        return OperationResult<ReadReceiptDto>.Success(receipt);
    }

    /// <summary>Lỗi vi phạm ràng buộc duy nhất của PostgreSQL (SqlState 23505).</summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
