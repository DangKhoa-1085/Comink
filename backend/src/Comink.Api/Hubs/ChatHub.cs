// Hub SignalR của chat. Nghiệp vụ nằm ở tầng Application/Infrastructure; hub chỉ lo
// xác thực, group và tín hiệu nhẹ (docs/02_tech_stack_decisions.md mục 1.4, 5.3).
using Comink.Application.Messaging;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Comink.Api.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Comink.Api.Hubs;

/// <summary>
/// Hub chat. Client kết nối tới <c>/hub/chat</c> và truyền access token qua
/// query string <c>access_token</c> (trình duyệt không gửi được header qua WebSocket).
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IConversationService _conversations;
    private readonly IUserConnectionRegistry _connections;
    private readonly IPresenceService _presence;
    private readonly IChatNotifier _notifier;
    private readonly ITypingThrottle _typingThrottle;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IConversationService conversations,
        IUserConnectionRegistry connections,
        IPresenceService presence,
        IChatNotifier notifier,
        ITypingThrottle typingThrottle,
        ILogger<ChatHub> logger)
    {
        _conversations = conversations;
        _connections = connections;
        _presence = presence;
        _notifier = notifier;
        _typingThrottle = typingThrottle;
        _logger = logger;
    }

    /// <summary>
    /// Khi một client kết nối: thêm vào nhóm riêng <c>user:{id}</c> và **tất cả** nhóm
    /// <c>conversation:{id}</c> mà người đó còn là thành viên; ghi nhận online và báo cho
    /// những người cùng hội thoại (NC Giai đoạn 7).
    /// <para>
    /// Group do server quyết định, không mở hub method cho client tự join — nhờ vậy người ngoài
    /// không thể tự đưa mình vào hội thoại (yêu cầu roadmap GĐ 3 #8, AGENTS.md mục 9.3).
    /// </para>
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            // Ghi nhận kết nối để có thể ngắt ngay khi tài khoản bị admin khóa (Giai đoạn 5, D2).
            _connections.Register(userGuid, Context);

            await Groups.AddToGroupAsync(Context.ConnectionId, ChatEvents.UserGroup(userGuid));

            var conversationIds = await _conversations.GetActiveConversationIdsAsync(userGuid);
            foreach (var conversationId in conversationIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ChatEvents.ConversationGroup(conversationId));
            }

            _logger.LogDebug(
                "Người dùng {UserId} kết nối hub, tham gia {Count} hội thoại",
                userGuid,
                conversationIds.Count);

            // Kết nối ĐẦU TIÊN → vừa online; báo cho những người cùng hội thoại.
            // Presence lỗi (Redis) không được làm hỏng việc kết nối hub.
            try
            {
                if (await _presence.ConnectAsync(userGuid, Context.ConnectionId))
                {
                    await BroadcastPresenceAsync(userGuid, isOnline: true, lastSeenAt: null);
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Không ghi nhận được trạng thái online của {UserId}", userGuid);
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>Gỡ kết nối khỏi sổ đăng ký khi client ngắt (kể cả khi bị server ngắt).</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            _connections.Unregister(userGuid, Context.ConnectionId);

            try
            {
                // Chỉ kết nối CUỐI CÙNG mới chuyển offline (nhiều tab/thiết bị).
                var lastSeenAt = await _presence.DisconnectAsync(userGuid, Context.ConnectionId);
                if (lastSeenAt is not null)
                {
                    await BroadcastPresenceAsync(userGuid, isOnline: false, lastSeenAt);
                }
            }
            catch (Exception presenceException)
            {
                _logger.LogWarning(presenceException, "Không ghi nhận được trạng thái offline của {UserId}", userGuid);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Tín hiệu "đang nhập" (NC Giai đoạn 7, quyết định D4): **không lưu DB**, chỉ chuyển tiếp
    /// cho các client khác trong hội thoại. Hub phải tự kiểm tra quyền — không tin id client gửi.
    /// </summary>
    public async Task Typing(Guid conversationId, bool isTyping)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userGuid))
        {
            return;
        }

        if (!await _conversations.IsMemberAsync(userGuid, conversationId))
        {
            // Người ngoài: bỏ qua im lặng, không để lộ sự tồn tại của hội thoại.
            return;
        }

        if (isTyping)
        {
            // Chặn spam: tối thiểu 2 giây giữa hai tín hiệu "bắt đầu gõ" của cùng một người.
            if (!_typingThrottle.TryAcquire(userGuid, conversationId))
            {
                return;
            }
        }
        else
        {
            _typingThrottle.Release(userGuid, conversationId);
        }

        // Trừ chính connection vừa gửi: người đang gõ không cần thấy "mình đang nhập".
        // Group hội thoại do server thêm lúc kết nối; hội thoại tạo sau đó sẽ nhận qua user:{id}
        // cho tin nhắn/đã đọc, còn tín hiệu tạm thời này chấp nhận không tới (xem phase7 mục 6).
        await Clients
            .GroupExcept(ChatEvents.ConversationGroup(conversationId), Context.ConnectionId)
            .SendAsync(ChatEvents.TypingChanged, new TypingSignalDto(conversationId, userGuid, isTyping));
    }

    /// <summary>
    /// Làm mới TTL presence. Client gọi mỗi 30 giây để trạng thái online không bị hết hạn
    /// trong khi kết nối vẫn sống (client chết thì TTL tự dọn — quyết định D1).
    /// </summary>
    public async Task Ping()
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userGuid))
        {
            await _presence.TouchAsync(userGuid, Context.ConnectionId);
        }
    }

    /// <summary>Tên nhóm riêng của một người dùng.</summary>
    public static string UserGroup(string userId) => ChatEvents.UserGroup(Guid.Parse(userId));

    /// <summary>
    /// Phát <c>PresenceChanged</c> cho những người **cùng hội thoại** với người vừa đổi trạng thái.
    /// Lỗi mạng/DB ở bước này chỉ ghi log — kết nối hub vẫn phải thành công.
    /// </summary>
    private async Task BroadcastPresenceAsync(Guid userId, bool isOnline, DateTimeOffset? lastSeenAt)
    {
        var audience = await _conversations.GetCoMemberUserIdsAsync(userId);
        if (audience.Count == 0)
        {
            return;
        }

        await _notifier.PresenceChangedAsync(
            audience,
            new PresenceDto(userId, isOnline, lastSeenAt));
    }
}
