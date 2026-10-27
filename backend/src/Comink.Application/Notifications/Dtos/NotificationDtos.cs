// DTO cho module Thông báo (docs/05_api_contract.md mục 10).
namespace Comink.Application.Notifications.Dtos;

/// <summary>Đăng ký token FCM của thiết bị hiện tại.</summary>
public record RegisterDeviceRequest(string PushToken, string Platform, string? DeviceName);

/// <summary>Thiết bị đã đăng ký (không trả lại token đầy đủ).</summary>
public record DeviceDto(Guid Id, string Platform, string? DeviceName, DateTimeOffset LastSeenAt);

/// <summary>Một thông báo trong app.</summary>
public record NotificationDto(
    Guid Id,
    string Type,
    Guid? ActorId,
    string EntityType,
    Guid? EntityId,
    string? Data,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
