// Hợp đồng phát sự kiện thời gian thực cho thông báo và trạng thái kết bạn qua SignalR.
using Comink.Application.Notifications.Dtos;

namespace Comink.Application.Notifications.Abstractions;

public interface INotificationNotifier
{
    Task NotificationCreatedAsync(
        Guid recipientId,
        NotificationDto notification,
        CancellationToken cancellationToken = default);

    Task FriendshipChangedAsync(
        IReadOnlyCollection<Guid> userIds,
        Guid? actorId,
        string action,
        CancellationToken cancellationToken = default);
}
