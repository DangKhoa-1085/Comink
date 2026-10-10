// Interface gửi push notification (Firebase Cloud Messaging)
namespace Comink.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng gửi push notification qua FCM.
/// </summary>
public interface IPushSender
{
    /// <summary>
    /// Gửi notification tới một token thiết bị cụ thể.
    /// </summary>
    Task SendToDeviceAsync(
        string deviceToken,
        PushNotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi notification tới nhiều token cùng lúc (tối đa 500 token).
    /// </summary>
    Task SendToDevicesAsync(
        IEnumerable<string> deviceTokens,
        PushNotification notification,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Nội dung một push notification.
/// </summary>
public record PushNotification(
    string Title,
    string Body,
    Dictionary<string, string>? Data = null,
    bool HighPriority = false);
