// Nghiệp vụ cuộc gọi (docs/05_api_contract.md mục 9, docs/tasks/phase4_calls.md).
using Comink.Application.Calls.Abstractions;
using Comink.Application.Calls.Dtos;
using Comink.Application.Common;
using Comink.Application.Common.Interfaces;
using Comink.Domain.Calls;
using Comink.Domain.Enums;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Comink.Infrastructure.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Comink.Infrastructure.Calls;

/// <summary>
/// Bắt đầu/trả lời/từ chối/kết thúc cuộc gọi, cấp token LiveKit ngắn hạn và đồng bộ trạng thái
/// từ webhook của nhà cung cấp.
/// </summary>
public class CallService : ICallService
{
    private readonly ApplicationDbContext _db;
    private readonly ICallProvider _provider;
    private readonly ICallNotifier _notifier;
    private readonly LiveKitOptions _liveKit;
    private readonly ILogger<CallService> _logger;

    public CallService(
        ApplicationDbContext db,
        ICallProvider provider,
        ICallNotifier notifier,
        IOptions<LiveKitOptions> liveKit,
        ILogger<CallService> logger)
    {
        _db = db;
        _provider = provider;
        _notifier = notifier;
        _liveKit = liveKit.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OperationResult<CallDto>> StartAsync(
        Guid currentUserId,
        StartCallRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<CallType>(request.Type, ignoreCase: true, out var callType))
        {
            return OperationResult<CallDto>.Failure(ErrorCodes.ValidationFailed);
        }

        List<Guid> memberIds;
        Guid conversationId;

        if (request.ConversationId is { } existingConversationId)
        {
            // Gọi 1–1: người nhận là thành viên còn lại của hội thoại.
            memberIds = await GetMemberIdsAsync(existingConversationId, cancellationToken);
            if (!memberIds.Contains(currentUserId))
            {
                return OperationResult<CallDto>.Failure(ErrorCodes.ConversationNotFound);
            }

            if (memberIds.Count < 2)
            {
                return OperationResult<CallDto>.Failure(ErrorCodes.ValidationFailed);
            }

            conversationId = existingConversationId;
        }
        else
        {
            // Gọi nhóm (quyết định D1): tạo conversation Group "ẩn" chỉ để chứa cuộc gọi.
            var invitees = (request.ParticipantIds ?? [])
                .Where(id => id != currentUserId)
                .Distinct()
                .ToList();

            if (!CallRules.AreParticipantsValid(invitees))
            {
                return OperationResult<CallDto>.Failure(ErrorCodes.ValidationFailed);
            }

            foreach (var invitee in invitees)
            {
                if (!await SocialLookup.IsActiveUserAsync(_db, invitee, cancellationToken))
                {
                    return OperationResult<CallDto>.Failure(ErrorCodes.NotFound);
                }

                if (!await SocialLookup.AreFriendsAsync(_db, currentUserId, invitee, cancellationToken))
                {
                    return OperationResult<CallDto>.Failure(ErrorCodes.MessagingNotFriends);
                }
            }

            var now = DateTimeOffset.UtcNow;
            var conversation = new Conversation
            {
                Type = ConversationType.Group,
                CreatedBy = currentUserId,
                CreatedAt = now
            };

            foreach (var userId in new[] { currentUserId }.Concat(invitees))
            {
                conversation.Members.Add(new ConversationMember
                {
                    ConversationId = conversation.Id,
                    UserId = userId,
                    Role = MemberRole.Member,
                    JoinedAt = now
                });
            }

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(cancellationToken);

            conversationId = conversation.Id;
            memberIds = await GetMemberIdsAsync(conversationId, cancellationToken);
        }

        // Mỗi hội thoại chỉ một cuộc gọi đang diễn ra (chống rò rỉ room).
        var hasActiveCall = await _db.Calls
            .AsNoTracking()
            .AnyAsync(
                c => c.ConversationId == conversationId &&
                     (c.Status == CallStatus.Ringing || c.Status == CallStatus.InProgress),
                cancellationToken);

        if (hasActiveCall)
        {
            return OperationResult<CallDto>.Failure(ErrorCodes.CallAlreadyActive);
        }

        var createdAt = DateTimeOffset.UtcNow;
        var call = new Call
        {
            ConversationId = conversationId,
            InitiatorId = currentUserId,
            Type = callType,
            Status = CallStatus.Ringing,
            CreatedAt = createdAt
        };

        // Tên room duy nhất, gắn với id cuộc gọi.
        call.RoomName = $"comink_{call.Id:N}";

        foreach (var userId in memberIds)
        {
            call.Participants.Add(new CallParticipant
            {
                CallId = call.Id,
                UserId = userId,
                Status = CallParticipantStatus.Invited,
                InvitedAt = createdAt
            });
        }

        _db.Calls.Add(call);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _provider.CreateRoomAsync(call.RoomName, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không tạo được room LiveKit cho cuộc gọi {CallId}", call.Id);
            call.Status = CallStatus.Failed;
            call.EndReason = CallEndReason.Failed;
            call.EndedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return OperationResult<CallDto>.Failure(ErrorCodes.CallProviderUnavailable);
        }

        var dto = await ToDtoAsync(call.Id, cancellationToken);
        var inviteeIds = memberIds.Where(id => id != currentUserId).ToList();
        await _notifier.CallIncomingAsync(inviteeIds, dto, cancellationToken);

        return OperationResult<CallDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<OperationResult<CallTokenDto>> GetTokenAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default)
    {
        var call = await FindCallForMemberAsync(callId, currentUserId, cancellationToken);
        if (call is null)
        {
            return OperationResult<CallTokenDto>.Failure(ErrorCodes.CallNotFound);
        }

        if (!CallRules.CanRequestToken(call.Status))
        {
            return OperationResult<CallTokenDto>.Failure(ErrorCodes.CallAlreadyEnded);
        }

        try
        {
            var displayName = await GetDisplayNameAsync(currentUserId, cancellationToken);
            var token = await _provider.GenerateAccessTokenAsync(
                call.RoomName,
                currentUserId.ToString(),
                displayName,
                canPublish: true,
                cancellationToken);

            // Client cần URL WebSocket; LiveKit JS chấp nhận ws:// / wss://.
            var url = _liveKit.Host
                .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');

            return OperationResult<CallTokenDto>.Success(new CallTokenDto(
                url,
                token,
                call.RoomName,
                DateTimeOffset.UtcNow.AddSeconds(CallRules.JoinTokenTtlSeconds)));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không cấp được token LiveKit cho cuộc gọi {CallId}", callId);
            return OperationResult<CallTokenDto>.Failure(ErrorCodes.CallProviderUnavailable);
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult<CallDto>> AnswerAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default)
    {
        var call = await FindCallForMemberAsync(callId, currentUserId, cancellationToken);
        if (call is null)
        {
            return OperationResult<CallDto>.Failure(ErrorCodes.CallNotFound);
        }

        if (CallRules.IsTerminal(call.Status))
        {
            return OperationResult<CallDto>.Failure(ErrorCodes.CallAlreadyEnded);
        }

        var participant = call.Participants.First(p => p.UserId == currentUserId);

        // Gọi lại nhiều lần (hoặc đã trả lời) → trả trạng thái hiện tại, không lỗi.
        if (participant.Status == CallParticipantStatus.Joined)
        {
            return OperationResult<CallDto>.Success(await ToDtoAsync(call.Id, cancellationToken));
        }

        var now = DateTimeOffset.UtcNow;
        participant.Status = CallParticipantStatus.Joined;
        participant.JoinedAt = now;
        call.AnsweredAt ??= now;
        call.Status = CallStatus.InProgress;

        await _db.SaveChangesAsync(cancellationToken);

        var dto = await ToDtoAsync(call.Id, cancellationToken);
        await _notifier.CallUpdatedAsync(call.Participants.Select(p => p.UserId).ToList(), dto, cancellationToken);
        return OperationResult<CallDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<OperationResult<CallDto>> DeclineAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default)
    {
        var call = await FindCallForMemberAsync(callId, currentUserId, cancellationToken);
        if (call is null)
        {
            return OperationResult<CallDto>.Failure(ErrorCodes.CallNotFound);
        }

        if (CallRules.IsTerminal(call.Status))
        {
            return OperationResult<CallDto>.Failure(ErrorCodes.CallAlreadyEnded);
        }

        var participant = call.Participants.First(p => p.UserId == currentUserId);
        if (participant.Status == CallParticipantStatus.Joined)
        {
            // Đã vào room thì phải dùng "kết thúc", không phải "từ chối".
            return OperationResult<CallDto>.Failure(ErrorCodes.ValidationFailed);
        }

        participant.Status = CallParticipantStatus.Declined;

        // Mọi người được mời đều từ chối và chưa ai vào → kết thúc cuộc gọi.
        var others = call.Participants.Where(p => p.UserId != call.InitiatorId).ToList();
        var allDeclined = others.All(p => p.Status is CallParticipantStatus.Declined or CallParticipantStatus.Missed);
        if (allDeclined && call.AnsweredAt is null)
        {
            return await FinishAsync(
                call,
                CallStatus.Declined,
                CallEndReason.Declined,
                notifyMembers: true,
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var dto = await ToDtoAsync(call.Id, cancellationToken);
        await _notifier.CallUpdatedAsync(call.Participants.Select(p => p.UserId).ToList(), dto, cancellationToken);
        return OperationResult<CallDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<OperationResult<CallDto>> EndAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default)
    {
        var call = await FindCallForMemberAsync(callId, currentUserId, cancellationToken);
        if (call is null)
        {
            return OperationResult<CallDto>.Failure(ErrorCodes.CallNotFound);
        }

        // Kết thúc hai lần (client + webhook) → idempotent.
        if (CallRules.IsTerminal(call.Status))
        {
            return OperationResult<CallDto>.Success(await ToDtoAsync(call.Id, cancellationToken));
        }

        var anyoneJoined = call.AnsweredAt is not null ||
                           call.Participants.Any(p => p.Status is CallParticipantStatus.Joined or CallParticipantStatus.Left);
        var cancelledByInitiator = !anyoneJoined && call.InitiatorId == currentUserId;

        var (status, reason) = CallRules.ResolveEnd(
            anyoneJoined,
            cancelledByInitiator,
            allDeclined: false,
            timedOut: false);

        return await FinishAsync(call, status, reason, notifyMembers: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<CallDto>> GetByIdAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default)
    {
        var call = await FindCallForMemberAsync(callId, currentUserId, cancellationToken);
        return call is null
            ? OperationResult<CallDto>.Failure(ErrorCodes.CallNotFound)
            : OperationResult<CallDto>.Success(await ToDtoAsync(call.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<CallSummaryDto>>> GetHistoryAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(limit, 1, 50);
        var decoded = CursorCodec.TryDecode(cursor);
        if (cursor is not null && decoded is null)
        {
            return OperationResult<PagedList<CallSummaryDto>>.Failure(ErrorCodes.MessageInvalidCursor);
        }

        var query = _db.CallParticipants
            .AsNoTracking()
            .Where(p => p.UserId == currentUserId);

        if (decoded is not null)
        {
            var cursorInvitedAt = decoded.Value.CreatedAt;
            var cursorCallId = decoded.Value.Id;
            query = query.Where(p =>
                p.InvitedAt < cursorInvitedAt ||
                (p.InvitedAt == cursorInvitedAt && p.CallId.CompareTo(cursorCallId) < 0));
        }

        var rows = await query
            .OrderByDescending(p => p.InvitedAt)
            .ThenByDescending(p => p.CallId)
            .Take(pageSize + 1)
            .Select(p => new
            {
                p.CallId,
                p.InvitedAt,
                p.Call.ConversationId,
                p.Call.Type,
                p.Call.Status,
                p.Call.EndReason,
                p.Call.CreatedAt,
                p.Call.DurationSeconds,
                p.Call.InitiatorId
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageRows = hasMore ? rows.Take(pageSize).ToList() : rows;

        var items = pageRows
            .Select(row => new CallSummaryDto(
                row.CallId,
                row.ConversationId,
                row.Type.ToString(),
                row.Status.ToString(),
                row.EndReason?.ToString(),
                row.CreatedAt,
                row.DurationSeconds,
                row.InitiatorId,
                row.InitiatorId == currentUserId))
            .ToList();

        var nextCursor = hasMore && pageRows.Count > 0
            ? CursorCodec.Encode(pageRows[^1].InvitedAt, pageRows[^1].CallId)
            : null;

        return OperationResult<PagedList<CallSummaryDto>>.Success(
            new PagedList<CallSummaryDto>(items, nextCursor, hasMore));
    }

    /// <inheritdoc />
    public async Task HandleProviderEventAsync(
        CallProviderEvent providerEvent,
        CancellationToken cancellationToken = default)
    {
        var call = await _db.Calls
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.RoomName == providerEvent.RoomName, cancellationToken);

        if (call is null)
        {
            // Sự kiện của room không do Comink tạo (hoặc đã dọn) → bỏ qua.
            _logger.LogDebug("Bỏ qua sự kiện LiveKit cho room lạ {Room}", providerEvent.RoomName);
            return;
        }

        switch (providerEvent.Type)
        {
            case CallProviderEventType.RoomStarted:
                // Room được tạo KHÔNG có nghĩa là có người nghe máy: `answered_at` chỉ được đặt
                // khi có participant_joined, nếu không cuộc gọi không ai nghe sẽ bị tính là
                // Completed thay vì Missed.
                await _db.SaveChangesAsync(cancellationToken);
                break;

            case CallProviderEventType.ParticipantJoined:
                MarkParticipant(call, providerEvent.ParticipantIdentity, joined: true);
                if (!CallRules.IsTerminal(call.Status))
                {
                    call.AnsweredAt ??= DateTimeOffset.UtcNow;
                    call.Status = CallStatus.InProgress;
                }

                await _db.SaveChangesAsync(cancellationToken);
                break;

            case CallProviderEventType.ParticipantLeft:
                MarkParticipant(call, providerEvent.ParticipantIdentity, joined: false);
                await _db.SaveChangesAsync(cancellationToken);
                break;

            case CallProviderEventType.RoomFinished:
                if (!CallRules.IsTerminal(call.Status))
                {
                    var anyoneJoined = call.AnsweredAt is not null ||
                                       call.Participants.Any(p =>
                                           p.Status is CallParticipantStatus.Joined or CallParticipantStatus.Left);
                    var (status, reason) = CallRules.ResolveEnd(
                        anyoneJoined,
                        cancelledByInitiator: false,
                        allDeclined: false,
                        timedOut: !anyoneJoined);

                    await FinishAsync(call, status, reason, notifyMembers: true, cancellationToken);
                }

                break;
        }
    }

    /// <summary>
    /// Đóng cuộc gọi: ghi thời lượng, xoá room ở nhà cung cấp (chống room treo), ghi tin nhắn
    /// loại <c>Call</c> vào hội thoại và phát sự kiện.
    /// </summary>
    private async Task<OperationResult<CallDto>> FinishAsync(
        Call call,
        CallStatus status,
        CallEndReason reason,
        bool notifyMembers,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        call.Status = status;
        call.EndReason = reason;
        call.EndedAt = now;
        call.DurationSeconds = CallRules.ComputeDurationSeconds(call.AnsweredAt, now);

        await _db.SaveChangesAsync(cancellationToken);

        // Room có thể đã tự xoá; EndRoomAsync không ném ra ngoài.
        await _provider.EndRoomAsync(call.RoomName, cancellationToken);

        await WriteCallMessageAsync(call, cancellationToken);

        var dto = await ToDtoAsync(call.Id, cancellationToken);

        if (notifyMembers)
        {
            var memberIds = call.Participants.Select(p => p.UserId).ToList();
            await _notifier.CallEndedAsync(
                memberIds,
                new CallEndedDto(call.Id, call.Status.ToString(), call.EndReason?.ToString(), call.DurationSeconds),
                cancellationToken);
            await _notifier.CallUpdatedAsync(memberIds, dto, cancellationToken);
        }

        return OperationResult<CallDto>.Success(dto);
    }

    /// <summary>
    /// Ghi một tin nhắn loại <c>Call</c> vào hội thoại (quyết định D4). Nội dung có dạng
    /// <c>call:{type}:{duration}:{endReason}</c> — client dịch sang vi/en.
    /// </summary>
    private async Task WriteCallMessageAsync(Call call, CancellationToken cancellationToken)
    {
        var already = await _db.Messages
            .AsNoTracking()
            .AnyAsync(m => m.CallId == call.Id, cancellationToken);

        if (already)
        {
            return;
        }

        var content = string.Join(':',
            "call",
            call.Type.ToString(),
            (call.DurationSeconds ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            (call.EndReason ?? CallEndReason.Completed).ToString());

        var message = new Message
        {
            ConversationId = call.ConversationId,
            SenderId = call.InitiatorId,
            Type = MessageType.Call,
            Content = content,
            CallId = call.Id,
            ClientMessageId = Guid.CreateVersion7(),
            CreatedAt = call.EndedAt ?? DateTimeOffset.UtcNow
        };

        _db.Messages.Add(message);

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == call.ConversationId, cancellationToken);

        if (conversation is not null)
        {
            conversation.LastMessageId = message.Id;
            conversation.LastMessageAt = message.CreatedAt;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Cập nhật trạng thái một người theo identity của LiveKit (thường là user id).</summary>
    private static void MarkParticipant(Call call, string? identity, bool joined)
    {
        if (!Guid.TryParse(identity, out var userId))
        {
            return;
        }

        var participant = call.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (joined)
        {
            participant.Status = CallParticipantStatus.Joined;
            participant.JoinedAt ??= now;
        }
        else if (participant.Status == CallParticipantStatus.Joined)
        {
            participant.Status = CallParticipantStatus.Left;
            participant.LeftAt = now;
        }
    }

    /// <summary>Tải cuộc gọi nếu người dùng là thành viên; ngược lại trả null (→ 404).</summary>
    private async Task<Call?> FindCallForMemberAsync(
        Guid callId,
        Guid currentUserId,
        CancellationToken cancellationToken) =>
        await _db.Calls
            .Include(c => c.Participants)
            .ThenInclude(p => p.User)
            .ThenInclude(u => u.Profile)
            .FirstOrDefaultAsync(
                c => c.Id == callId && c.Participants.Any(p => p.UserId == currentUserId),
                cancellationToken);

    private Task<List<Guid>> GetMemberIdsAsync(Guid conversationId, CancellationToken cancellationToken) =>
        _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

    private async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var name = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Profile == null ? u.UserName : u.Profile.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(name) ? userId.ToString() : name;
    }

    private async Task<CallDto> ToDtoAsync(Guid callId, CancellationToken cancellationToken)
    {
        var call = await _db.Calls
            .AsNoTracking()
            .Include(c => c.Participants)
            .ThenInclude(p => p.User)
            .ThenInclude(u => u.Profile)
            .FirstAsync(c => c.Id == callId, cancellationToken);

        var participants = call.Participants
            .OrderBy(p => p.InvitedAt)
            .Select(p => new CallParticipantDto(
                p.UserId,
                p.User.Profile?.DisplayName ?? p.User.UserName ?? string.Empty,
                p.Status.ToString(),
                p.InvitedAt))
            .ToList();

        return new CallDto(
            call.Id,
            call.ConversationId,
            call.Type.ToString(),
            call.Status.ToString(),
            call.RoomName,
            call.InitiatorId,
            call.CreatedAt,
            call.AnsweredAt,
            call.EndedAt,
            call.DurationSeconds,
            call.EndReason?.ToString(),
            participants);
    }
}
