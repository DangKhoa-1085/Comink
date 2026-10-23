// Lưu các kết nối SignalR đang mở theo người dùng để có thể ngắt khi tài khoản bị khóa.
// SignalR không cung cấp API "ngắt kết nối của người khác"; cách duy nhất được hỗ trợ là
// giữ HubCallerContext từ OnConnectedAsync rồi gọi Abort() (Giai đoạn 5, quyết định D2).
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Comink.Api.Realtime;

/// <summary>Sổ đăng ký kết nối SignalR theo người dùng (singleton).</summary>
public interface IUserConnectionRegistry
{
    /// <summary>Ghi nhận một kết nối vừa mở của người dùng.</summary>
    void Register(Guid userId, HubCallerContext context);

    /// <summary>Gỡ kết nối khi client ngắt.</summary>
    void Unregister(Guid userId, string connectionId);

    /// <summary>Ảnh chụp các kết nối đang mở của người dùng (an toàn khi duyệt song song).</summary>
    IReadOnlyList<HubCallerContext> GetConnections(Guid userId);
}

/// <inheritdoc />
public sealed class UserConnectionRegistry : IUserConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, HubCallerContext>> _connections = new();

    /// <inheritdoc />
    public void Register(Guid userId, HubCallerContext context)
    {
        var userConnections = _connections.GetOrAdd(
            userId,
            _ => new ConcurrentDictionary<string, HubCallerContext>());

        userConnections[context.ConnectionId] = context;
    }

    /// <inheritdoc />
    public void Unregister(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var userConnections))
        {
            return;
        }

        userConnections.TryRemove(connectionId, out _);

        // Dọn khóa rỗng để dictionary không phình theo số người dùng đã offline.
        if (userConnections.IsEmpty)
        {
            _connections.TryRemove(userId, out _);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HubCallerContext> GetConnections(Guid userId) =>
        _connections.TryGetValue(userId, out var userConnections)
            ? userConnections.Values.ToArray()
            : [];
}
