// Test cho ChatHub: khi kết nối, server tự thêm client vào group riêng và các group hội thoại
// mà người đó là thành viên (yêu cầu roadmap GĐ 3 #8; AGENTS.md mục 9.3); đồng thời ghi nhận
// presence và chuyển tiếp tín hiệu "đang nhập" (NC Giai đoạn 7).
using System.Security.Claims;
using Comink.Api.Hubs;
using Comink.Api.Realtime;
using Comink.Application.Common;
using Comink.Application.Messaging;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class ChatHubTests
{
    private sealed class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string Group)> Added { get; } = [];

        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeHubCallerContext(string? userIdentifier) : HubCallerContext
    {
        public override string ConnectionId => "connection-1";
        public override string? UserIdentifier => userIdentifier;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort()
        {
        }
    }

    private sealed class FakeClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> Sent { get; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Sent.Add((method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubClients : IHubCallerClients
    {
        public FakeClientProxy Proxy { get; } = new();

        public IClientProxy Caller => Proxy;
        public IClientProxy Others => Proxy;
        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy OthersInGroup(string groupName) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) => Proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    /// <summary>Chỉ cần các phương thức hub dùng; các thành phần khác không có ở đây.</summary>
    private sealed class StubConversationService(
        IReadOnlyList<Guid> conversationIds,
        IReadOnlyList<Guid>? coMemberIds = null) : IConversationService
    {
        public bool IsMember { get; set; } = true;

        public Task<IReadOnlyList<Guid>> GetActiveConversationIdsAsync(
            Guid currentUserId,
            CancellationToken cancellationToken = default) => Task.FromResult(conversationIds);

        public Task<IReadOnlyList<Guid>> GetCoMemberUserIdsAsync(
            Guid currentUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(coMemberIds ?? []);

        public Task<bool> IsMemberAsync(
            Guid currentUserId,
            Guid conversationId,
            CancellationToken cancellationToken = default) => Task.FromResult(IsMember);

        public Task<OperationResult<ReadReceiptDto>> MarkReadAsync(
            Guid currentUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<ConversationOpenResult>> OpenDirectAsync(
            Guid currentUserId, Guid targetUserId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<PagedList<ConversationSummaryDto>>> GetMyConversationsAsync(
            Guid currentUserId, string? cursor, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<ConversationDto>> GetByIdAsync(
            Guid currentUserId, Guid conversationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<ConversationDto>> CreateGroupAsync(
            Guid currentUserId, CreateGroupConversationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class AlwaysAllowedTypingThrottle : ITypingThrottle
    {
        public bool TryAcquire(Guid userId, Guid conversationId) => true;

        public void Release(Guid userId, Guid conversationId)
        {
        }
    }

    private static (ChatHub Hub, FakeGroupManager Groups, FakePresenceService Presence, RecordingChatNotifier Notifier,
        FakeHubClients Clients)
        CreateHub(
            string? userIdentifier,
            IReadOnlyList<Guid> conversationIds,
            IReadOnlyList<Guid>? coMemberIds = null)
    {
        var groups = new FakeGroupManager();
        var presence = new FakePresenceService();
        var notifier = new RecordingChatNotifier();
        var clients = new FakeHubClients();
        var stub = new StubConversationService(conversationIds, coMemberIds);

        var hub = new ChatHub(
            stub,
            new UserConnectionRegistry(),
            presence,
            notifier,
            new AlwaysAllowedTypingThrottle(),
            NullLogger<ChatHub>.Instance)
        {
            Context = new FakeHubCallerContext(userIdentifier),
            Groups = groups,
            Clients = clients
        };

        return (hub, groups, presence, notifier, clients);
    }

    [Fact]
    public async Task OnConnected_AddsUserGroupAndEveryConversationGroup()
    {
        var userId = Guid.CreateVersion7();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var (hub, groups, _, _, _) = CreateHub(userId.ToString(), [first, second]);

        await hub.OnConnectedAsync();

        Assert.Equal(3, groups.Added.Count);
        Assert.All(groups.Added, entry => Assert.Equal("connection-1", entry.ConnectionId));
        Assert.Contains(groups.Added, entry => entry.Group == $"user:{userId}");
        Assert.Contains(groups.Added, entry => entry.Group == $"conversation:{first}");
        Assert.Contains(groups.Added, entry => entry.Group == $"conversation:{second}");
    }

    [Fact]
    public async Task OnConnected_WithoutUserIdentifier_AddsNoGroup()
    {
        var (hub, groups, _, _, _) = CreateHub(null, [Guid.CreateVersion7()]);

        await hub.OnConnectedAsync();

        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task OnConnected_WithNonGuidUserIdentifier_AddsNoGroup()
    {
        // Token không mang user id hợp lệ thì không được tham gia bất kỳ group nào.
        var (hub, groups, _, _, _) = CreateHub("khong-phai-guid", [Guid.CreateVersion7()]);

        await hub.OnConnectedAsync();

        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task OnConnected_UserWithoutConversations_JoinsOnlyUserGroup()
    {
        var userId = Guid.CreateVersion7();
        var (hub, groups, _, _, _) = CreateHub(userId.ToString(), []);

        await hub.OnConnectedAsync();

        var entry = Assert.Single(groups.Added);
        Assert.Equal($"user:{userId}", entry.Group);
    }

    [Fact]
    public async Task OnConnected_FirstConnection_BroadcastsOnlineToCoMembers()
    {
        var userId = Guid.CreateVersion7();
        var friendId = Guid.CreateVersion7();
        var (hub, _, presence, notifier, _) = CreateHub(userId.ToString(), [], [friendId]);
        presence.ConnectTransitionsToOnline = true;

        await hub.OnConnectedAsync();

        Assert.Contains(userId, presence.Connected);
        var presenceCall = Assert.Single(notifier.Presences);
        Assert.Contains(friendId, presenceCall.Audience);
        Assert.Equal(userId, presenceCall.Presence.UserId);
        Assert.True(presenceCall.Presence.IsOnline);
    }

    [Fact]
    public async Task OnConnected_AdditionalTab_DoesNotBroadcastOnlineAgain()
    {
        var userId = Guid.CreateVersion7();
        var (hub, _, presence, notifier, _) = CreateHub(userId.ToString(), [], [Guid.CreateVersion7()]);
        presence.ConnectTransitionsToOnline = false;

        await hub.OnConnectedAsync();

        Assert.Contains(userId, presence.Connected);
        Assert.Empty(notifier.Presences);
    }

    [Fact]
    public async Task OnDisconnected_LastConnection_BroadcastsOfflineWithLastSeen()
    {
        var userId = Guid.CreateVersion7();
        var friendId = Guid.CreateVersion7();
        var (hub, _, presence, notifier, _) = CreateHub(userId.ToString(), [], [friendId]);
        var lastSeen = DateTimeOffset.UtcNow;
        presence.DisconnectLastSeen = lastSeen;

        await hub.OnDisconnectedAsync(null);

        Assert.Contains(userId, presence.Disconnected);
        var presenceCall = Assert.Single(notifier.Presences);
        Assert.Equal(userId, presenceCall.Presence.UserId);
        Assert.False(presenceCall.Presence.IsOnline);
        Assert.Equal(lastSeen, presenceCall.Presence.LastSeenAt);
    }

    [Fact]
    public async Task OnDisconnected_RemainingTab_DoesNotBroadcastOffline()
    {
        var userId = Guid.CreateVersion7();
        var (hub, _, presence, notifier, _) = CreateHub(userId.ToString(), [], [Guid.CreateVersion7()]);
        presence.DisconnectLastSeen = null;

        await hub.OnDisconnectedAsync(null);

        Assert.Contains(userId, presence.Disconnected);
        Assert.Empty(notifier.Presences);
    }

    [Fact]
    public async Task Typing_ForMember_IsForwardedToConversationGroupExceptSender()
    {
        var userId = Guid.CreateVersion7();
        var conversationId = Guid.CreateVersion7();
        var (hub, _, _, _, clients) = CreateHub(userId.ToString(), [conversationId]);

        await hub.Typing(conversationId, true);

        var sent = Assert.Single(clients.Proxy.Sent);
        Assert.Equal(ChatEvents.TypingChanged, sent.Method);
        var signal = Assert.IsType<TypingSignalDto>(sent.Args[0]);
        Assert.Equal(conversationId, signal.ConversationId);
        Assert.Equal(userId, signal.UserId);
        Assert.True(signal.IsTyping);
    }

    [Fact]
    public async Task Typing_ForNonMember_IsSilentlyIgnored()
    {
        var userId = Guid.CreateVersion7();
        var stub = new StubConversationService([]) { IsMember = false };
        var clients = new FakeHubClients();
        var hub = new ChatHub(
            stub,
            new UserConnectionRegistry(),
            new FakePresenceService(),
            new RecordingChatNotifier(),
            new AlwaysAllowedTypingThrottle(),
            NullLogger<ChatHub>.Instance)
        {
            Context = new FakeHubCallerContext(userId.ToString()),
            Groups = new FakeGroupManager(),
            Clients = clients
        };

        await hub.Typing(Guid.CreateVersion7(), true);

        // Không được để lộ hội thoại: không phát gì cho người ngoài.
        Assert.Empty(clients.Proxy.Sent);
    }

    [Fact]
    public async Task Ping_RefreshesPresenceTtl()
    {
        var userId = Guid.CreateVersion7();
        var (hub, _, presence, _, _) = CreateHub(userId.ToString(), []);

        await hub.Ping();

        Assert.Contains(userId, presence.Touched);
    }

    [Fact]
    public void TypingThrottle_AllowsFirstSignalThenThrottlesUntilRelease()
    {
        var throttle = new TypingThrottle();
        var userId = Guid.CreateVersion7();
        var conversationId = Guid.CreateVersion7();

        Assert.True(throttle.TryAcquire(userId, conversationId));
        Assert.False(throttle.TryAcquire(userId, conversationId));

        // Ngừng gõ → lần gõ kế tiếp được phát ngay, không phải chờ hết 2 giây.
        throttle.Release(userId, conversationId);
        Assert.True(throttle.TryAcquire(userId, conversationId));

        // Mỗi hội thoại một khoá riêng.
        Assert.True(throttle.TryAcquire(userId, Guid.CreateVersion7()));
    }

    [Fact]
    public void TypingThrottle_IsScopedPerUser()
    {
        var throttle = new TypingThrottle();
        var conversationId = Guid.CreateVersion7();

        Assert.True(throttle.TryAcquire(Guid.CreateVersion7(), conversationId));
        Assert.True(throttle.TryAcquire(Guid.CreateVersion7(), conversationId));
    }
}
