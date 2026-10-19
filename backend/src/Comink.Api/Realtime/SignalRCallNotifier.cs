// Cài đặt ICallNotifier bằng SignalR — thuộc tầng Api để Infrastructure không phụ thuộc SignalR.
using Comink.Api.Hubs;
using Comink.Application.Calls;
using Comink.Application.Calls.Abstractions;
using Comink.Application.Calls.Dtos;
using Comink.Application.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace Comink.Api.Realtime;

/// <summary>
/// Phát sự kiện cuộc gọi tới <c>user:{id}</c> của từng người liên quan.
/// Không dùng group hội thoại: hội thoại (nhất là hội thoại nhóm ẩn của cuộc gọi) có thể được
/// tạo **sau** khi client kết nối — bài học từ GĐ 3.
/// </summary>
public class SignalRCallNotifier : ICallNotifier
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<SignalRCallNotifier> _logger;

    public SignalRCallNotifier(IHubContext<ChatHub> hub, ILogger<SignalRCallNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task CallIncomingAsync(
        IReadOnlyList<Guid> inviteeIds,
        CallDto call,
        CancellationToken cancellationToken = default) =>
        SendAsync(inviteeIds, CallEvents.CallIncoming, call, cancellationToken);

    public Task CallUpdatedAsync(
        IReadOnlyList<Guid> memberIds,
        CallDto call,
        CancellationToken cancellationToken = default) =>
        SendAsync(memberIds, CallEvents.CallUpdated, call, cancellationToken);

    public Task CallEndedAsync(
        IReadOnlyList<Guid> memberIds,
        CallEndedDto ended,
        CancellationToken cancellationToken = default) =>
        SendAsync(memberIds, CallEvents.CallEnded, ended, cancellationToken);

    private async Task SendAsync(
        IReadOnlyList<Guid> userIds,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var userId in userIds.Distinct())
            {
                await _hub.Clients
                    .Group(ChatEvents.UserGroup(userId))
                    .SendAsync(eventName, payload, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Cuộc gọi đã được ghi DB; lỗi phát sóng không được làm hỏng thao tác.
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho cuộc gọi", eventName);
        }
    }
}
