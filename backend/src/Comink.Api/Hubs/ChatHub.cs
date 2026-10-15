// Hub SignalR nền của Giai đoạn 1: chỉ cần xác thực được bằng access token.
// Nghiệp vụ chat (nhóm theo hội thoại, gửi/nhận tin) thuộc Giai đoạn 3.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Comink.Api.Hubs;

/// <summary>
/// Hub chat. Client kết nối tới <c>/hub/chat</c> và truyền access token qua
/// query string <c>access_token</c> (trình duyệt không gửi được header qua WebSocket).
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    /// <summary>
    /// Khi một client kết nối thành công, đưa vào nhóm riêng <c>user:{id}</c>
    /// để nhận thông báo và trạng thái cuộc gọi đến (docs/02_tech_stack_decisions.md mục 1.4).
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>Tên nhóm riêng của một người dùng.</summary>
    public static string UserGroup(string userId) => $"user:{userId}";
}
