// Cài đặt IChatNotifier bằng SignalR — thuộc tầng Api để Infrastructure không phụ thuộc SignalR.
using Comink.Api.Hubs;
using Comink.Application.Messaging;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace Comink.Api.Realtime;

/// <summary>Phát sự kiện chat tới các thành viên của hội thoại.</summary>
public class SignalRChatNotifier : IChatNotifier
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<SignalRChatNotifier> _logger;

    public SignalRChatNotifier(IHubContext<ChatHub> hub, ILogger<SignalRChatNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task MessageCreatedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        MessageDto message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) Group của hội thoại (chỉ chứa client đã kết nối khi hội thoại đã tồn tại).
            await _hub.Clients
                .Group(ChatEvents.ConversationGroup(conversationId))
                .SendAsync(ChatEvents.MessageCreated, message, cancellationToken);

            // 2) Group riêng của từng thành viên — đường phát CHÍNH, luôn đúng kể cả khi hội thoại
            //    vừa được tạo sau lúc client kết nối. Thiếu bước này thì phải tải lại trang mới thấy tin.
            foreach (var userId in memberUserIds.Distinct())
            {
                await _hub.Clients
                    .Group(ChatEvents.UserGroup(userId))
                    .SendAsync(ChatEvents.MessageCreated, message, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Tin đã lưu vào DB; lỗi phát sóng không được làm client tưởng gửi thất bại.
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho hội thoại {ConversationId}",
                ChatEvents.MessageCreated, conversationId);
        }
    }
}
