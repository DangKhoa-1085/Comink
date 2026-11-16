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

    /// <inheritdoc />
    public async Task MessageReadAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        ReadReceiptDto receipt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Cùng chiến lược với MessageCreated: group hội thoại + user:{id} của từng thành viên,
            // để hội thoại tạo sau lúc kết nối vẫn tới được người gửi.
            await _hub.Clients
                .Group(ChatEvents.ConversationGroup(conversationId))
                .SendAsync(ChatEvents.MessagesRead, receipt, cancellationToken);

            foreach (var userId in memberUserIds.Distinct())
            {
                await _hub.Clients
                    .Group(ChatEvents.UserGroup(userId))
                    .SendAsync(ChatEvents.MessagesRead, receipt, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Mốc đã lưu DB; lỗi phát sóng chỉ ảnh hưởng hiển thị tạm thời.
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho hội thoại {ConversationId}",
                ChatEvents.MessagesRead, conversationId);
        }
    }

    /// <inheritdoc />
    public async Task PresenceChangedAsync(
        IReadOnlyList<Guid> audienceUserIds,
        PresenceDto presence,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Chỉ gửi cho người cùng hội thoại; bỏ chính người vừa đổi trạng thái.
            var recipients = audienceUserIds
                .Where(userId => userId != presence.UserId)
                .Distinct()
                .Select(userId => userId.ToString())
                .ToArray();

            if (recipients.Length == 0)
            {
                return;
            }

            await _hub.Clients
                .Users(recipients)
                .SendAsync(ChatEvents.PresenceChanged, presence, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không phát được sự kiện {Event} của người dùng {UserId}",
                ChatEvents.PresenceChanged, presence.UserId);
        }
    }
}
