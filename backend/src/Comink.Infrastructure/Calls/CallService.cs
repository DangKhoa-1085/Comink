// Nghiệp vụ cuộc gọi (docs/05_api_contract.md mục 9, docs/tasks/phase4_calls.md).
using Comink.Application.Calls.Abstractions;
using Comink.Application.Calls.Dtos;
using Comink.Application.Common;
using Comink.Application.Common.Interfaces;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Notifications.Abstractions;
using Comink.Domain.Calls;
using Comink.Domain.Enums;
using Comink.Infrastructure.Messaging;
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
    private readonly INotificationService? _notifications;
    private readonly IChatNotifier? _chatNotifier;

    public CallService(
        ApplicationDbContext db,
        ICallProvider provider,
        ICallNotifier notifier,
        IOptions<LiveKitOptions> liveKit,
        ILogger<CallService> logger,
        INotificationService? notifications = null,
        IChatNotifier? chatNotifier = null)
    {
        _db = db;
        _provider = provider;
        _notifier = notifier;
        _liveKit = liveKit.Value;
        _logger = logger;
        _notifications = notifications;
        _chatNotifier = chatNotifier;
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

        // Quyết định D6: cuộc gọi luôn bắt đầu trong một hội thoại (1–1 hoặc nhóm).
        var conversationId = request.ConversationId;
        var memberIds = await GetMemberIdsAsync(conversationId, cancellationToken);

        if (!memberIds.Contains(currentUserId))
        {
            return OperationResult<CallDto>.Failure(ErrorCodes.ConversationNotFound);
        }

        if (memberIds.Count < 2)
        {
            return OperationResult<CallDto>.Failure(ErrorCodes.ValidationFailed);
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

        // Push cho người được gọi khi app đóng/không mở SignalR.
        if (_notifications is not null && inviteeIds.Count > 0)
        {
            try
            {
                var initiatorName = await _db.UserProfiles
                    .AsNoTracking()
                    .Where(p => p.UserId == currentUserId)
                    .Select(p => p.DisplayName)
                    .FirstOrDefaultAsync(cancellationToken) ?? "Ai đó";
                await _notifications.PushToUsersAsync(
                    inviteeIds,
                    "Cuộc gọi đến",
                    $"{initiatorName} đang gọi {(callType == CallType.Video ? "video" : "thoại")}",
                    new Dictionary<string, string>
                    {
                        ["type"] = "CallIncoming",
                        ["callId"] = call.Id.ToString(),
                        ["conversationId"] = conversationId.ToString(),
                        ["callType"] = callType.ToString(),
                    },
                    highPriority: true,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Không gửi được push cuộc gọi {CallId}", call.Id);
            }
        }

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

            // Client cần URL WebSocket; ưu tiên LiveKit:PublicUrl cho thiết bị/emulator (D7).
            var url = _liveKit.ClientUrl;

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

        // Hết giờ đổ chuông (mặc định 30s) → đóng cuộc gọi là Missed/NoAnswer, không cho vào room.
        if (call.Status == CallStatus.Ringing &&
            call.CreatedAt < DateTimeOffset.UtcNow.AddSeconds(-CallRules.RingTimeoutSeconds))
        {
            MarkInvitedAsMissed(call);
            await FinishAsync(call, CallStatus.Missed, CallEndReason.NoAnswer, notifyMembers: true, cancellationToken);
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

        var anyoneJoined = HasNonInitiatorAnswered(call);
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
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        if (normalizedFilter is not ("all" or "missed"))
        {
            return OperationResult<PagedList<CallSummaryDto>>.Failure(ErrorCodes.ValidationFailed);
        }

        var pageSize = Math.Clamp(limit, 1, 50);
        var decoded = CursorCodec.TryDecode(cursor);
        if (cursor is not null && decoded is null)
        {
            return OperationResult<PagedList<CallSummaryDto>>.Failure(ErrorCodes.MessageInvalidCursor);
        }

        var query = _db.CallParticipants
            .AsNoTracking()
            .Where(p => p.UserId == currentUserId);

        // Lọc "bị lỡ" phía server để phân trang vẫn đúng (không lọc trên một trang đã tải).
        if (normalizedFilter == "missed")
        {
            query = query.Where(p => p.Status == CallParticipantStatus.Missed);
        }

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
                MyStatus = p.Status,
                p.Call.ConversationId,
                ConversationType = p.Call.Conversation.Type,
                ConversationTitle = p.Call.Conversation.Title,
                p.Call.Type,
                p.Call.Status,
                p.Call.EndReason,
                p.Call.CreatedAt,
                p.Call.AnsweredAt,
                p.Call.EndedAt,
                p.Call.DurationSeconds,
                p.Call.InitiatorId,
                InitiatorDisplayName = p.Call.Initiator.Profile != null
                    ? p.Call.Initiator.Profile.DisplayName
                    : p.Call.Initiator.UserName
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageRows = hasMore ? rows.Take(pageSize).ToList() : rows;

        // Người còn lại của hội thoại 1–1: gom một truy vấn cho cả trang (không N+1).
        var directConversationIds = pageRows
            .Where(row => row.ConversationType == ConversationType.Direct)
            .Select(row => row.ConversationId)
            .Distinct()
            .ToList();

        var peersByConversation = new Dictionary<Guid, CallPeerDto>();
        if (directConversationIds.Count > 0)
        {
            var peerRows = await _db.ConversationMembers
                .AsNoTracking()
                .Where(m => directConversationIds.Contains(m.ConversationId) &&
                            m.UserId != currentUserId)
                .Select(m => new
                {
                    m.ConversationId,
                    m.UserId,
                    DisplayName = m.User.Profile != null ? m.User.Profile.DisplayName : m.User.UserName,
                    AvatarUrl = m.User.Profile != null ? m.User.Profile.AvatarUrl : null
                })
                .ToListAsync(cancellationToken);

            foreach (var group in peerRows.GroupBy(row => row.ConversationId))
            {
                var peer = group.First();
                peersByConversation[group.Key] = new CallPeerDto(
                    peer.UserId,
                    peer.DisplayName ?? string.Empty,
                    peer.AvatarUrl);
            }
        }

        var items = pageRows
            .Select(row =>
            {
                peersByConversation.TryGetValue(row.ConversationId, out var peer);
                return new CallSummaryDto(
                    row.CallId,
                    row.ConversationId,
                    row.Type.ToString(),
                    row.Status.ToString(),
                    row.EndReason?.ToString(),
                    row.CreatedAt,
                    row.DurationSeconds,
                    row.InitiatorId,
                    row.InitiatorId == currentUserId,
                    row.MyStatus.ToString(),
                    row.MyStatus == CallParticipantStatus.Missed,
                    row.ConversationType.ToString(),
                    row.ConversationTitle,
                    peer,
                    row.InitiatorDisplayName ?? string.Empty,
                    row.AnsweredAt,
                    row.EndedAt);
            })
            .ToList();

        var nextCursor = hasMore && pageRows.Count > 0
            ? CursorCodec.Encode(pageRows[^1].InvitedAt, pageRows[^1].CallId)
            : null;

        return OperationResult<PagedList<CallSummaryDto>>.Success(
            new PagedList<CallSummaryDto>(items, nextCursor, hasMore));
    }

    /// <inheritdoc />
    public async Task<int> TimeoutRingingCallsAsync(CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(-CallRules.RingTimeoutSeconds);

        var expiredCalls = await _db.Calls
            .Include(c => c.Participants)
            .Where(c => c.Status == CallStatus.Ringing && c.CreatedAt < deadline)
            .ToListAsync(cancellationToken);

        var finished = 0;

        foreach (var call in expiredCalls)
        {
            // Chỉ người KHÔNG phải người gọi vào room mới tính là "có người nghe".
            // (Người gọi thường vào room ngay để chờ máy — xem quyết định D1.)
            if (HasNonInitiatorAnswered(call))
            {
                continue;
            }

            MarkInvitedAsMissed(call);
            await FinishAsync(call, CallStatus.Missed, CallEndReason.NoAnswer, notifyMembers: true, cancellationToken);
            finished++;
        }

        if (finished > 0)
        {
            _logger.LogInformation("Đã kết thúc {Count} cuộc gọi hết giờ đổ chuông", finished);
        }

        return finished;
    }

    /// <summary>Đánh dấu những người được mời nhưng chưa trả lời là <c>Missed</c> (trừ người gọi).</summary>
    private static void MarkInvitedAsMissed(Call call)
    {
        foreach (var participant in call.Participants.Where(p =>
                     p.UserId != call.InitiatorId && p.Status == CallParticipantStatus.Invited))
        {
            participant.Status = CallParticipantStatus.Missed;
        }
    }

    /// <summary>
    /// Đã có người nghe chưa? Chỉ tính người **không phải người gọi** đã vào room — quyết định D1
    /// (Ưu tiên 3). Người gọi vào room trước để chờ máy không làm cuộc gọi thành <c>InProgress</c>.
    /// </summary>
    private static bool HasNonInitiatorAnswered(Call call) =>
        call.AnsweredAt is not null ||
        call.Participants.Any(p =>
            p.UserId != call.InitiatorId &&
            p.Status is CallParticipantStatus.Joined or CallParticipantStatus.Left);

    /// <summary>
    /// Tạo thông báo + push <see cref="NotificationType.MissedCall"/> cho những người đã bỏ lỡ
    /// cuộc gọi (quyết định D3, Ưu tiên 3). Kiểm tra <c>entity_id = call_id</c> để không tạo trùng
    /// khi webhook và client cùng kết thúc cuộc gọi.
    /// </summary>
    private async Task NotifyMissedParticipantsAsync(Call call, CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        var missedUserIds = call.Participants
            .Where(p => p.UserId != call.InitiatorId && p.Status == CallParticipantStatus.Missed)
            .Select(p => p.UserId)
            .Distinct()
            .ToList();

        if (missedUserIds.Count == 0)
        {
            return;
        }

        var alreadyNotified = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.Type == NotificationType.MissedCall &&
                        n.EntityId == call.Id &&
                        missedUserIds.Contains(n.RecipientId))
            .Select(n => n.RecipientId)
            .ToListAsync(cancellationToken);

        var targets = missedUserIds.Except(alreadyNotified).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var initiatorName = await GetDisplayNameAsync(call.InitiatorId, cancellationToken);
        var callKind = call.Type == CallType.Video ? "video" : "thoại";
        var body = $"{initiatorName} đã gọi {callKind} cho bạn";

        foreach (var userId in targets)
        {
            await _notifications.CreateAndPushAsync(
                userId,
                call.InitiatorId,
                NotificationType.MissedCall,
                "Call",
                call.Id,
                "Cuộc gọi nhỡ",
                body,
                new Dictionary<string, string>
                {
                    ["type"] = "CallMissed",
                    ["callId"] = call.Id.ToString(),
                    ["conversationId"] = call.ConversationId.ToString(),
                    ["callType"] = call.Type.ToString(),
                },
                cancellationToken);
        }
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
            {
                var parsed = Guid.TryParse(providerEvent.ParticipantIdentity, out var joinedUserId);
                MarkParticipant(call, providerEvent.ParticipantIdentity, joined: true);

                // Quyết định D1 (Ưu tiên 3): người gọi vào room trước để chờ máy KHÔNG có nghĩa
                // là cuộc gọi đã được nghe. Chỉ khi một người KHÁC người gọi tham gia mới đặt
                // answered_at / InProgress — nếu không, mọi cuộc gọi sẽ thành InProgress ngay khi
                // bắt đầu và không bao giờ hết giờ đổ chuông (Missed), đồng hồ thời lượng cũng sai.
                if (parsed && joinedUserId != call.InitiatorId && !CallRules.IsTerminal(call.Status))
                {
                    call.AnsweredAt ??= DateTimeOffset.UtcNow;
                    call.Status = CallStatus.InProgress;
                }

                await _db.SaveChangesAsync(cancellationToken);
                break;
            }

            case CallProviderEventType.ParticipantLeft:
                MarkParticipant(call, providerEvent.ParticipantIdentity, joined: false);
                await _db.SaveChangesAsync(cancellationToken);
                break;

            case CallProviderEventType.RoomFinished:
                if (!CallRules.IsTerminal(call.Status))
                {
                    var anyoneJoined = HasNonInitiatorAnswered(call);
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

        // Cuộc gọi nhỡ: người gọi không phải "người bị nhỡ". Đánh dấu ở đây để mọi nhánh kết
        // thúc (timeout, webhook room_finished, answer quá giờ) có cùng trạng thái participant.
        if (status == CallStatus.Missed)
        {
            MarkInvitedAsMissed(call);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Room có thể đã tự xoá; EndRoomAsync không ném ra ngoài.
        await _provider.EndRoomAsync(call.RoomName, cancellationToken);

        var callMessage = await WriteCallMessageAsync(call, cancellationToken);

        if (status == CallStatus.Missed)
        {
            await NotifyMissedParticipantsAsync(call, cancellationToken);
        }

        var dto = await ToDtoAsync(call.Id, cancellationToken);

        if (notifyMembers)
        {
            var memberIds = call.Participants.Select(p => p.UserId).ToList();
            if (callMessage is not null && _chatNotifier is not null)
            {
                await _chatNotifier.MessageCreatedAsync(
                    call.ConversationId,
                    memberIds,
                    MessagingMapping.ToMessageDto(callMessage),
                    cancellationToken);
            }

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
    private async Task<Message?> WriteCallMessageAsync(Call call, CancellationToken cancellationToken)
    {
        var already = await _db.Messages
            .AsNoTracking()
            .AnyAsync(m => m.CallId == call.Id, cancellationToken);

        if (already)
        {
            return null;
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
        return message;
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
