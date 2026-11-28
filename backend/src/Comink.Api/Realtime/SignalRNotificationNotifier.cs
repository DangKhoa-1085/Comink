// Phát sự kiện SignalR cho thông báo và trạng thái kết bạn tới nhóm user:{id}.
using Comink.Api.Hubs;
using Comink.Application.Messaging;
using Comink.Application.Notifications;
using Comink.Application.Notifications.Abstractions;
using Comink.Application.Notifications.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace Comink.Api.Realtime;

/// <summary>Cài đặt <see cref="INotificationNotifier"/> dùng SignalR Hub chung (<c>/hub/chat</c>).</summary>
public class SignalRNotificationNotifier : INotificationNotifier
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<SignalRNotificationNotifier> _logger;

    public SignalRNotificationNotifier(
        IHubContext<ChatHub> hub,
        ILogger<SignalRNotificationNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotificationCreatedAsync(
        Guid recipientId,
        NotificationDto notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients
                .Group(ChatEvents.UserGroup(recipientId))
                .SendAsync(NotificationEvents.NotificationCreated, notification, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho thông báo {NotificationId}",
                NotificationEvents.NotificationCreated, notification.Id);
        }
    }

    /// <inheritdoc />
    public async Task FriendshipChangedAsync(
        IReadOnlyCollection<Guid> userIds,
        Guid? actorId,
        string action,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        try
        {
            var groups = userIds.Distinct().Select(ChatEvents.UserGroup).ToList();
            await _hub.Clients
                .Groups(groups)
                .SendAsync(NotificationEvents.FriendshipChanged, new { actorId, action }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không phát được sự kiện {Event} với hành động {Action}",
                NotificationEvents.FriendshipChanged, action);
        }
    }
}
