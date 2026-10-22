// Nghiệp vụ hội thoại (docs/05_api_contract.md mục 8.2–8.4).
using Comink.Application.Common;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Enums;
using Comink.Domain.Messaging;
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

    public ConversationService(ApplicationDbContext db)
    {
        _db = db;
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

        if (!await SocialLookup.IsActiveUserAsync(_db, targetUserId, cancellationToken))
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
            if (!await SocialLookup.IsActiveUserAsync(_db, inviteeId, cancellationToken))
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

        var items = pageRows.Select(row =>
        {
            // Chỉ hội thoại 1–1 mới có "peer"; nhóm hiển thị theo Title + số thành viên.
            var peer = row.Type == ConversationType.Direct
                ? peers.FirstOrDefault(p => p.ConversationId == row.Id)
                : null;
            var last = lastMessages.FirstOrDefault(m => m.Id == row.LastMessageId);
            var memberCount = memberCounts.FirstOrDefault(x => x.ConversationId == row.Id)?.Count ?? 0;

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
                        peer.AvatarUrl),
                last is null
                    ? null
                    : new ConversationLastMessageDto(
                        last.Id,
                        last.SenderId,
                        MessagingMapping.Preview(last.Content),
                        last.Type.ToString(),
                        last.CreatedAt),
                // Số tin chưa đọc là [NC] ở GĐ 3 — luôn 0.
                0,
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

        var members = conversation.Members
            .OrderBy(m => m.JoinedAt)
            .Select(m => new ConversationMemberDto(
                m.UserId,
                m.User.UserName ?? string.Empty,
                m.User.Profile?.DisplayName ?? m.User.UserName ?? string.Empty,
                m.User.Profile?.AvatarUrl))
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

    /// <summary>Lỗi vi phạm ràng buộc duy nhất của PostgreSQL (SqlState 23505).</summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
