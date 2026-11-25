// Fake dùng chung cho test tích hợp phần nhắn tin nâng cao (NC Giai đoạn 7).
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;

namespace Comink.Api.IntegrationTests;

/// <summary>
/// Presence giả: không cần Redis. Mặc định coi mọi người offline; test đặt trạng thái
/// hoặc cấu hình kết quả chuyển trạng thái của <c>ConnectAsync</c>/<c>DisconnectAsync</c>.
/// </summary>
internal sealed class FakePresenceService : IPresenceService
{
    private readonly Dictionary<Guid, PresenceDto> _statuses = [];

    public List<Guid> Connected { get; } = [];

    public List<Guid> Disconnected { get; } = [];

    public List<Guid> Touched { get; } = [];

    /// <summary>Có phải kết nối đầu tiên (offline → online) hay không.</summary>
    public bool ConnectTransitionsToOnline { get; set; }

    /// <summary>Thời điểm ngắt nếu đây là kết nối cuối cùng; null = còn kết nối khác.</summary>
    public DateTimeOffset? DisconnectLastSeen { get; set; }

    public void SetStatus(Guid userId, bool isOnline, DateTimeOffset? lastSeenAt = null) =>
        _statuses[userId] = new PresenceDto(userId, isOnline, lastSeenAt);

    public Task<bool> ConnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        Connected.Add(userId);
        SetStatus(userId, true);
        return Task.FromResult(ConnectTransitionsToOnline);
    }

    public Task TouchAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        Touched.Add(userId);
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> DisconnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        Disconnected.Add(userId);
        if (DisconnectLastSeen is null)
        {
            return Task.FromResult<DateTimeOffset?>(null);
        }

        SetStatus(userId, false, DisconnectLastSeen);
        return Task.FromResult(DisconnectLastSeen);
    }

    public Task<IReadOnlyDictionary<Guid, PresenceDto>> GetStatusesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, PresenceDto>>(
            userIds.Distinct().ToDictionary(
                userId => userId,
                userId => _statuses.GetValueOrDefault(userId) ?? new PresenceDto(userId, false, null)));
}

/// <summary>Không phát gì — test nghiệp vụ không cần SignalR.</summary>
internal sealed class NullChatNotifier : IChatNotifier
{
    public Task MessageCreatedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        MessageDto message,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task MessageReadAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        ReadReceiptDto receipt,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task MessageUpdatedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        MessageDto message,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PinnedMessagesChangedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        PinnedMessagesDto pinned,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PresenceChangedAsync(
        IReadOnlyList<Guid> audienceUserIds,
        PresenceDto presence,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Ghi lại các lần phát sự kiện để khẳng định danh sách người nhận.</summary>
internal sealed class RecordingChatNotifier : IChatNotifier
{
    public List<(Guid ConversationId, IReadOnlyList<Guid> Members, Guid MessageId)> Messages { get; } = [];

    public List<(Guid ConversationId, IReadOnlyList<Guid> Members, ReadReceiptDto Receipt)> Reads { get; } = [];

    public List<(IReadOnlyList<Guid> Audience, PresenceDto Presence)> Presences { get; } = [];

    public List<(Guid ConversationId, IReadOnlyList<Guid> Members, MessageDto Message)> Updates { get; } = [];

    public List<(Guid ConversationId, IReadOnlyList<Guid> Members, PinnedMessagesDto Pinned)> PinnedUpdates { get; } = [];

    public Task MessageCreatedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        MessageDto message,
        CancellationToken cancellationToken = default)
    {
        Messages.Add((conversationId, memberUserIds, message.Id));
        return Task.CompletedTask;
    }

    public Task MessageReadAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        ReadReceiptDto receipt,
        CancellationToken cancellationToken = default)
    {
        Reads.Add((conversationId, memberUserIds, receipt));
        return Task.CompletedTask;
    }

    public Task MessageUpdatedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        MessageDto message,
        CancellationToken cancellationToken = default)
    {
        Updates.Add((conversationId, memberUserIds, message));
        return Task.CompletedTask;
    }

    public Task PinnedMessagesChangedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        PinnedMessagesDto pinned,
        CancellationToken cancellationToken = default)
    {
        PinnedUpdates.Add((conversationId, memberUserIds, pinned));
        return Task.CompletedTask;
    }

    public Task PresenceChangedAsync(
        IReadOnlyList<Guid> audienceUserIds,
        PresenceDto presence,
        CancellationToken cancellationToken = default)
    {
        Presences.Add((audienceUserIds, presence));
        return Task.CompletedTask;
    }
}
