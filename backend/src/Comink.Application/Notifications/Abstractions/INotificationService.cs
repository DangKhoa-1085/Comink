// Hợp đồng dịch vụ thông báo: thiết bị, hộp thư và push FCM.
using Comink.Application.Common;
using Comink.Application.Notifications.Dtos;
using Comink.Domain.Enums;

namespace Comink.Application.Notifications.Abstractions;

/// <summary>Quản lý token thiết bị, hộp thư thông báo và gửi push.</summary>
public interface INotificationService
{
    Task<OperationResult<DeviceDto>> RegisterDeviceAsync(
        Guid currentUserId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult<bool>> UnregisterDeviceAsync(
        Guid currentUserId,
        string pushToken,
        CancellationToken cancellationToken = default);

    Task<OperationResult<PagedList<NotificationDto>>> GetListAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<OperationResult<bool>> MarkReadAsync(
        Guid currentUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>Gửi push tới mọi thiết bị còn hoạt động của các người dùng; không tạo dòng DB.</summary>
    Task PushToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        bool highPriority,
        CancellationToken cancellationToken = default);

    /// <summary>Tạo thông báo trong app rồi gửi push tới người nhận.</summary>
    Task CreateAndPushAsync(
        Guid recipientId,
        Guid? actorId,
        NotificationType type,
        string entityType,
        Guid? entityId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken = default);
}
