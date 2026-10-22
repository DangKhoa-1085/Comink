// Test cho ChatHub: khi kết nối, server tự thêm client vào group riêng và các group hội thoại
// mà người đó là thành viên (yêu cầu roadmap GĐ 3 #8; AGENTS.md mục 9.3).
using System.Security.Claims;
using Comink.Api.Hubs;
using Comink.Application.Common;
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

    /// <summary>Chỉ cần GetActiveConversationIdsAsync; các thành phần khác không dùng ở hub.</summary>
    private sealed class StubConversationService(IReadOnlyList<Guid> conversationIds) : IConversationService
    {
        public Task<IReadOnlyList<Guid>> GetActiveConversationIdsAsync(
            Guid currentUserId,
            CancellationToken cancellationToken = default) => Task.FromResult(conversationIds);

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

    private static (ChatHub Hub, FakeGroupManager Groups) CreateHub(
        string? userIdentifier,
        IReadOnlyList<Guid> conversationIds)
    {
        var groups = new FakeGroupManager();
        var hub = new ChatHub(new StubConversationService(conversationIds), NullLogger<ChatHub>.Instance)
        {
            Context = new FakeHubCallerContext(userIdentifier),
            Groups = groups
        };

        return (hub, groups);
    }

    [Fact]
    public async Task OnConnected_AddsUserGroupAndEveryConversationGroup()
    {
        var userId = Guid.CreateVersion7();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var (hub, groups) = CreateHub(userId.ToString(), [first, second]);

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
        var (hub, groups) = CreateHub(null, [Guid.CreateVersion7()]);

        await hub.OnConnectedAsync();

        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task OnConnected_WithNonGuidUserIdentifier_AddsNoGroup()
    {
        // Token không mang user id hợp lệ thì không được tham gia bất kỳ group nào.
        var (hub, groups) = CreateHub("khong-phai-guid", [Guid.CreateVersion7()]);

        await hub.OnConnectedAsync();

        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task OnConnected_UserWithoutConversations_JoinsOnlyUserGroup()
    {
        var userId = Guid.CreateVersion7();
        var (hub, groups) = CreateHub(userId.ToString(), []);

        await hub.OnConnectedAsync();

        var entry = Assert.Single(groups.Added);
        Assert.Equal($"user:{userId}", entry.Group);
    }
}
