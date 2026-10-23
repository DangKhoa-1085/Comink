// Hub SignalR của chat. Nghiệp vụ nằm ở tầng Application/Infrastructure; hub chỉ lo
// xác thực, group và tín hiệu nhẹ (docs/02_tech_stack_decisions.md mục 1.4, 5.3).
using Comink.Application.Messaging;
using Comink.Application.Messaging.Abstractions;
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
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IConversationService conversations,
        IUserConnectionRegistry connections,
        ILogger<ChatHub> logger)
    {
        _conversations = conversations;
        _connections = connections;
        _logger = logger;
    }

    /// <summary>
    /// Khi một client kết nối: thêm vào nhóm riêng <c>user:{id}</c> và **tất cả** nhóm
    /// <c>conversation:{id}</c> mà người đó còn là thành viên.
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
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Tên nhóm riêng của một người dùng.</summary>
    public static string UserGroup(string userId) => ChatEvents.UserGroup(Guid.Parse(userId));
}
