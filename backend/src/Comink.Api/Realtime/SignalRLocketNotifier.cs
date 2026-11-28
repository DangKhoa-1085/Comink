// Phát sự kiện SignalR cho Comink Locket tới các nhóm user:{id} của tác giả và bạn bè được phép xem.
using Comink.Api.Hubs;
using Comink.Application.Messaging;
using Comink.Application.Social;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace Comink.Api.Realtime;

/// <summary>Cài đặt <see cref="ILocketNotifier"/> dùng SignalR Hub chung (<c>/hub/chat</c>).</summary>
public class SignalRLocketNotifier : ILocketNotifier
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<SignalRLocketNotifier> _logger;

    public SignalRLocketNotifier(IHubContext<ChatHub> hub, ILogger<SignalRLocketNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LocketCreatedAsync(
        IReadOnlyList<Guid> viewerUserIds,
        LocketDto moment,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserIds.Count == 0)
        {
            return;
        }

        try
        {
            var groups = viewerUserIds.Distinct().Select(ChatEvents.UserGroup).ToList();
            await _hub.Clients
                .Groups(groups)
                .SendAsync(LocketEvents.LocketCreated, moment, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho Locket {MomentId}",
                LocketEvents.LocketCreated, moment.Id);
        }
    }

    /// <inheritdoc />
    public async Task LocketUpdatedAsync(
        IReadOnlyList<Guid> viewerUserIds,
        Guid momentId,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserIds.Count == 0)
        {
            return;
        }

        try
        {
            var groups = viewerUserIds.Distinct().Select(ChatEvents.UserGroup).ToList();
            await _hub.Clients
                .Groups(groups)
                .SendAsync(LocketEvents.LocketUpdated, new { momentId }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho Locket {MomentId}",
                LocketEvents.LocketUpdated, momentId);
        }
    }

    /// <inheritdoc />
    public async Task LocketDeletedAsync(
        IReadOnlyList<Guid> viewerUserIds,
        Guid momentId,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserIds.Count == 0)
        {
            return;
        }

        try
        {
            var groups = viewerUserIds.Distinct().Select(ChatEvents.UserGroup).ToList();
            await _hub.Clients
                .Groups(groups)
                .SendAsync(LocketEvents.LocketDeleted, new { momentId }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không phát được sự kiện {Event} cho Locket {MomentId}",
                LocketEvents.LocketDeleted, momentId);
        }
    }
}
