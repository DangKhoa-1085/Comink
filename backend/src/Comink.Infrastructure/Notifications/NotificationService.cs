// Dịch vụ thông báo: đăng ký thiết bị, hộp thư và gửi push FCM.
using System.Text.Json;
using Comink.Application.Common;
using Comink.Application.Common.Interfaces;
using Comink.Application.Notifications.Abstractions;
using Comink.Application.Notifications.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Comink.Infrastructure.Notifications;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IPushSender _push;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext db,
        IPushSender push,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _push = push;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OperationResult<DeviceDto>> RegisterDeviceAsync(
        Guid currentUserId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var device = await _db.UserDevices
            .FirstOrDefaultAsync(d => d.PushToken == request.PushToken, cancellationToken);

        if (device is null)
        {
            device = new UserDevice
            {
                UserId = currentUserId,
                Platform = request.Platform,
                PushToken = request.PushToken,
                DeviceName = request.DeviceName,
                IsActive = true,
                CreatedAt = now,
                LastSeenAt = now,
            };
            _db.UserDevices.Add(device);
        }
        else
        {
            // Cùng token có thể được đăng nhập lại bởi tài khoản khác trên cùng thiết bị.
            device.UserId = currentUserId;
            device.Platform = request.Platform;
            device.DeviceName = request.DeviceName;
            device.IsActive = true;
            device.LastSeenAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return OperationResult<DeviceDto>.Success(
            new DeviceDto(device.Id, device.Platform, device.DeviceName, device.LastSeenAt));
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> UnregisterDeviceAsync(
        Guid currentUserId,
        string pushToken,
        CancellationToken cancellationToken = default)
    {
        var device = await _db.UserDevices
            .FirstOrDefaultAsync(
                d => d.PushToken == pushToken && d.UserId == currentUserId,
                cancellationToken);

        if (device is not null)
        {
            device.IsActive = false;
            device.LastSeenAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<NotificationDto>>> GetListAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(limit, 1, 50);
        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == currentUserId);

        var decoded = CursorCodec.TryDecode(cursor);
        if (decoded is not null)
        {
            var cursorCreatedAt = decoded.Value.CreatedAt;
            var cursorId = decoded.Value.Id;
            query = query.Where(n =>
                n.CreatedAt < cursorCreatedAt ||
                (n.CreatedAt == cursorCreatedAt && n.Id.CompareTo(cursorId) < 0));
        }

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageRows = hasMore ? rows.Take(pageSize).ToList() : rows;
        var items = pageRows
            .Select(n => new NotificationDto(
                n.Id,
                n.Type.ToString(),
                n.ActorId,
                n.EntityType,
                n.EntityId,
                n.Data,
                n.IsRead,
                n.CreatedAt,
                n.ReadAt))
            .ToList();

        var nextCursor = hasMore && pageRows.Count > 0
            ? CursorCodec.Encode(pageRows[^1].CreatedAt, pageRows[^1].Id)
            : null;

        return OperationResult<PagedList<NotificationDto>>.Success(
            new PagedList<NotificationDto>(items, nextCursor, hasMore));
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> MarkReadAsync(
        Guid currentUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(
                n => n.Id == notificationId && n.RecipientId == currentUserId,
                cancellationToken);

        if (notification is null)
        {
            return OperationResult<bool>.Failure(ErrorCodes.NotFound);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task PushToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        bool highPriority,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var distinctIds = userIds.Distinct().ToList();
        var tokens = await _db.UserDevices
            .AsNoTracking()
            .Where(d => distinctIds.Contains(d.UserId) && d.IsActive)
            .Select(d => d.PushToken)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            return;
        }

        var payload = new PushNotification(
            title,
            body,
            data?.ToDictionary(item => item.Key, item => item.Value),
            highPriority);

        try
        {
            foreach (var chunk in tokens.Chunk(500))
            {
                await _push.SendToDevicesAsync(chunk, payload, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Push lỗi không được làm hỏng thao tác nghiệp vụ (tin nhắn đã lưu).
            _logger.LogError(exception, "Không gửi được push tới {Count} thiết bị", tokens.Count);
        }
    }

    /// <inheritdoc />
    public async Task CreateAndPushAsync(
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
        var notification = new Notification
        {
            RecipientId = recipientId,
            ActorId = actorId,
            Type = type,
            EntityType = entityType,
            EntityId = entityId,
            Data = data is null ? null : JsonSerializer.Serialize(data),
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        await PushToUsersAsync([recipientId], title, body, data, false, cancellationToken);
    }
}
