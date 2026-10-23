// Cài đặt IAccountNotifier bằng SignalR — thuộc tầng Api (Application không tham chiếu SignalR).
using Comink.Api.Hubs;
using Comink.Application.Admin;
using Comink.Application.Admin.Abstractions;
using Comink.Application.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace Comink.Api.Realtime;

/// <summary>
/// Thông báo tài khoản bị khóa tới client và ngắt các kết nối SignalR đang mở của người đó.
/// </summary>
public class SignalRAccountNotifier : IAccountNotifier
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly IUserConnectionRegistry _connections;
    private readonly ILogger<SignalRAccountNotifier> _logger;

    public SignalRAccountNotifier(
        IHubContext<ChatHub> hub,
        IUserConnectionRegistry connections,
        ILogger<SignalRAccountNotifier> logger)
    {
        _hub = hub;
        _connections = connections;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AccountLockedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // 1) Gửi sự kiện trước để client chủ động xóa phiên và hiện thông báo.
        //    Gửi lỗi không được chặn bước 2 — bảo mật không phụ thuộc việc client nhận được tin.
        try
        {
            await _hub.Clients
                .Group(ChatEvents.UserGroup(userId))
                .SendAsync(AdminEvents.AccountLocked, new { userId }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Không gửi được sự kiện {Event} cho người dùng {UserId}",
                AdminEvents.AccountLocked,
                userId);
        }

        // 2) Ngắt mọi kết nối SignalR đang mở — kể cả client không xử lý sự kiện ở bước 1.
        var connections = _connections.GetConnections(userId);
        foreach (var connection in connections)
        {
            try
            {
                connection.Abort();
            }
            catch (Exception exception)
            {
                _logger.LogDebug(
                    exception,
                    "Không ngắt được kết nối {ConnectionId} của người dùng {UserId}",
                    connection.ConnectionId,
                    userId);
            }
        }

        _logger.LogInformation(
            "Đã ngắt {ConnectionCount} kết nối SignalR của tài khoản bị khóa {UserId}",
            connections.Count,
            userId);
    }
}
