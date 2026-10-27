// Fake INotificationService cho test nghiệp vụ: ghi lại push và thông báo đã tạo.
using Comink.Application.Common;
using Comink.Application.Notifications.Abstractions;
using Comink.Application.Notifications.Dtos;
using Comink.Domain.Enums;

namespace Comink.Api.IntegrationTests;

public sealed class FakeNotificationService : INotificationService
{
    public sealed record PushRecord(
        IReadOnlyCollection<Guid> UserIds,
        string Title,
        string Body,
        IReadOnlyDictionary<string, string>? Data,
        bool HighPriority);

    public sealed record CreateRecord(
        Guid RecipientId,
        Guid? ActorId,
        NotificationType Type,
        string EntityType,
        Guid? EntityId,
        string Title,
        string Body);

    public List<PushRecord> Pushes { get; } = [];
    public List<CreateRecord> Creates { get; } = [];

    public Task<OperationResult<DeviceDto>> RegisterDeviceAsync(
        Guid currentUserId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult<DeviceDto>.Success(
            new DeviceDto(Guid.CreateVersion7(), request.Platform, request.DeviceName, DateTimeOffset.UtcNow)));

    public Task<OperationResult<bool>> UnregisterDeviceAsync(
        Guid currentUserId,
        string pushToken,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult<bool>.Success(true));

    public Task<OperationResult<PagedList<NotificationDto>>> GetListAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult<PagedList<NotificationDto>>.Success(
            new PagedList<NotificationDto>([], null, false)));

    public Task<OperationResult<bool>> MarkReadAsync(
        Guid currentUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult<bool>.Success(true));

    public Task PushToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        bool highPriority,
        CancellationToken cancellationToken = default)
    {
        Pushes.Add(new PushRecord(userIds, title, body, data, highPriority));
        return Task.CompletedTask;
    }

    public Task CreateAndPushAsync(
        Guid recipientId,
        Guid? actorId,
        NotificationType type,
        string entityType,
        Guid? entityId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken = default)
    {
        Creates.Add(new CreateRecord(recipientId, actorId, type, entityType, entityId, title, body));
        Pushes.Add(new PushRecord([recipientId], title, body, data, false));
        return Task.CompletedTask;
    }
}
