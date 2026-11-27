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
            // Gom group hội thoại + group từng thành viên thành một lần phát sóng qua backplane/SignalR.
            var groups = BuildTargetGroups(conversationId, memberUserIds);
            await _hub.Clients
                .Groups(groups)
                .SendAsync(ChatEvents.MessageCreated, message, cancellationToken);
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
            var groups = BuildTargetGroups(conversationId, memberUserIds);
            await _hub.Clients
                .Groups(groups)
                .SendAsync(ChatEvents.MessagesRead, receipt, cancellationToken);
        }
        catch (Exception exception)
        {
            // Mốc đã lưu DB; lỗi phát sóng chỉ ảnh hưởng hiển thị tạm thời.
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho hội thoại {ConversationId}",
                ChatEvents.MessagesRead, conversationId);
        }
    }

    /// <inheritdoc />
    public async Task MessageUpdatedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        MessageDto message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groups = BuildTargetGroups(conversationId, memberUserIds);
            await _hub.Clients
                .Groups(groups)
                .SendAsync(ChatEvents.MessageUpdated, message, cancellationToken);
        }
        catch (Exception exception)
        {
            // Thay đổi đã lưu DB; lỗi phát sóng chỉ ảnh hưởng hiển thị tạm thời.
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho hội thoại {ConversationId}",
                ChatEvents.MessageUpdated, conversationId);
        }
    }

    /// <inheritdoc />
    public async Task PinnedMessagesChangedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        PinnedMessagesDto pinned,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groups = BuildTargetGroups(conversationId, memberUserIds);
            await _hub.Clients
                .Groups(groups)
                .SendAsync(ChatEvents.PinnedMessagesChanged, pinned, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho hội thoại {ConversationId}",
                ChatEvents.PinnedMessagesChanged, conversationId);
        }
    }

    private static List<string> BuildTargetGroups(Guid conversationId, IReadOnlyList<Guid> memberUserIds)
    {
        var groups = new List<string>(1 + memberUserIds.Count)
        {
            ChatEvents.ConversationGroup(conversationId)
        };
        foreach (var userId in memberUserIds.Distinct())
        {
            groups.Add(ChatEvents.UserGroup(userId));
        }

        return groups;
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
