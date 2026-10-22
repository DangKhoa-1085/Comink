// Test tích hợp cho cuộc gọi: bắt đầu, cấp token (chặn người ngoài), trả lời, kết thúc,
// ghi tin nhắn Call, xử lý webhook room_finished — docs/05_api_contract.md mục 9.
using Comink.Application.Calls.Abstractions;
using Comink.Application.Calls.Dtos;
using Comink.Application.Common;
using Comink.Application.Common.Interfaces;
using Comink.Domain.Enums;
using Comink.Infrastructure.Calls;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class CallServiceTests : IntegrationTestBase
{
    public CallServiceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    /// <summary>Nhà cung cấp gọi giả — không gọi LiveKit thật trong test.</summary>
    private sealed class FakeCallProvider : ICallProvider
    {
        public List<string> CreatedRooms { get; } = [];
        public List<string> EndedRooms { get; } = [];

        public Task<string> CreateRoomAsync(string roomName, CancellationToken cancellationToken = default)
        {
            CreatedRooms.Add(roomName);
            return Task.FromResult(roomName);
        }

        public Task<string> GenerateAccessTokenAsync(
            string roomName,
            string userId,
            string displayName,
            bool canPublish = true,
            CancellationToken cancellationToken = default) => Task.FromResult($"token-{userId}-{roomName}");

        public Task EndRoomAsync(string roomName, CancellationToken cancellationToken = default)
        {
            EndedRooms.Add(roomName);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCallNotifier : ICallNotifier
    {
        public List<(IReadOnlyList<Guid> Users, Guid CallId)> Incoming { get; } = [];
        public List<Guid> Updated { get; } = [];
        public List<Guid> Ended { get; } = [];

        public Task CallIncomingAsync(
            IReadOnlyList<Guid> inviteeIds, CallDto call, CancellationToken cancellationToken = default)
        {
            Incoming.Add((inviteeIds, call.Id));
            return Task.CompletedTask;
        }

        public Task CallUpdatedAsync(
            IReadOnlyList<Guid> memberIds, CallDto call, CancellationToken cancellationToken = default)
        {
            Updated.Add(call.Id);
            return Task.CompletedTask;
        }

        public Task CallEndedAsync(
            IReadOnlyList<Guid> memberIds, CallEndedDto ended, CancellationToken cancellationToken = default)
        {
            Ended.Add(ended.CallId);
            return Task.CompletedTask;
        }
    }

    private static ApplicationUser NewUser(string userName, string displayName) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        Email = $"{userName}@test.local",
        NormalizedEmail = $"{userName}@test.local".ToUpperInvariant(),
        EmailConfirmed = true,
        CreatedAt = DateTimeOffset.UtcNow,
        Profile = new UserProfile { DisplayName = displayName },
        Settings = new UserSettings()
    };

    private static CallService NewService(
        ApplicationDbContext db,
        FakeCallProvider provider,
        RecordingCallNotifier notifier) =>
        new(
            db,
            provider,
            notifier,
            Options.Create(new LiveKitOptions { Host = "http://localhost:7880", ApiKey = "devkey", ApiSecret = new string('s', 32) }),
            NullLogger<CallService>.Instance);

    /// <summary>Hai người là bạn bè + một hội thoại 1–1 sẵn có.</summary>
    private async Task<(ApplicationDbContext Db, ApplicationUser First, ApplicationUser Second, Conversation Conversation)>
        CreateDirectPairAsync()
    {
        var db = await CreateDbContextAsync();
        var first = NewUser("alice", "Alice");
        var second = NewUser("bob", "Bob");
        db.Users.AddRange(first, second);
        db.Friendships.Add(new Friendship
        {
            RequesterId = first.Id,
            AddresseeId = second.Id,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var conversation = new Conversation
        {
            Type = ConversationType.Direct,
            DirectKey = Comink.Domain.Messaging.ConversationRules.BuildDirectKey(first.Id, second.Id),
            CreatedBy = first.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        conversation.Members.Add(new ConversationMember { ConversationId = conversation.Id, UserId = first.Id, JoinedAt = conversation.CreatedAt });
        conversation.Members.Add(new ConversationMember { ConversationId = conversation.Id, UserId = second.Id, JoinedAt = conversation.CreatedAt });
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        return (db, first, second, conversation);
    }

    [Fact]
    public async Task Start_DirectCall_CreatesRingingCallAndNotifiesInvitee()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var provider = new FakeCallProvider();
        var notifier = new RecordingCallNotifier();

        var result = await NewService(db, provider, notifier)
            .StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));

        Assert.True(result.Succeeded);
        Assert.Equal("Ringing", result.Value!.Status);
        Assert.Equal("Audio", result.Value.Type);
        Assert.Equal(2, result.Value.Participants.Count);
        Assert.Single(provider.CreatedRooms);

        // Người gọi không nhận sự kiện "cuộc gọi đến", chỉ người được mời nhận.
        var incoming = Assert.Single(notifier.Incoming);
        Assert.Contains(bob.Id, incoming.Users);
        Assert.DoesNotContain(alice.Id, incoming.Users);
    }

    [Fact]
    public async Task Start_WhenCallAlreadyActiveInConversation_IsRejected()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var provider = new FakeCallProvider();
        var notifier = new RecordingCallNotifier();
        var service = NewService(db, provider, notifier);

        await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));
        var second = await service.StartAsync(bob.Id, new StartCallRequest(conversation.Id, "Video"));

        Assert.False(second.Succeeded);
        Assert.Equal(ErrorCodes.CallAlreadyActive, second.ErrorCode);
    }

    [Fact]
    public async Task Start_GroupConversationCall_InvitesEveryMember()
    {
        var (db, alice, bob, _) = await CreateDirectPairAsync();
        var carol = NewUser("carol", "Carol");
        db.Users.Add(carol);
        db.Friendships.Add(new Friendship
        {
            RequesterId = alice.Id,
            AddresseeId = carol.Id,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // Nhóm chat thật (NC GĐ 3) — cuộc gọi nhóm bắt đầu từ đây (quyết định D6, không còn nhóm ẩn).
        var group = new Conversation
        {
            Type = ConversationType.Group,
            Title = "Nhom hoc tap",
            CreatedBy = alice.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var userId in new[] { alice.Id, bob.Id, carol.Id })
        {
            group.Members.Add(new ConversationMember
            {
                ConversationId = group.Id,
                UserId = userId,
                JoinedAt = group.CreatedAt
            });
        }

        db.Conversations.Add(group);
        await db.SaveChangesAsync();

        var notifier = new RecordingCallNotifier();
        var result = await NewService(db, new FakeCallProvider(), notifier)
            .StartAsync(alice.Id, new StartCallRequest(group.Id, "Video"));

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Value!.Participants.Count);

        // Cả hai người còn lại đều nhận "cuộc gọi đến", người gọi thì không.
        var incoming = Assert.Single(notifier.Incoming);
        Assert.Contains(bob.Id, incoming.Users);
        Assert.Contains(carol.Id, incoming.Users);
        Assert.DoesNotContain(alice.Id, incoming.Users);
    }

    [Fact]
    public async Task GetToken_ForOutsider_IsNotFound()
    {
        var (db, alice, _, conversation) = await CreateDirectPairAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var service = NewService(db, new FakeCallProvider(), new RecordingCallNotifier());
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));

        var token = await service.GetTokenAsync(outsider.Id, call.Value!.Id);

        Assert.False(token.Succeeded);
        Assert.Equal(ErrorCodes.CallNotFound, token.ErrorCode);
    }

    [Fact]
    public async Task GetToken_ForMember_ReturnsShortLivedTokenAndWsUrl()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var service = NewService(db, new FakeCallProvider(), new RecordingCallNotifier());
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Video"));

        var token = await service.GetTokenAsync(bob.Id, call.Value!.Id);

        Assert.True(token.Succeeded);
        Assert.StartsWith("ws://", token.Value!.Url);
        Assert.Equal(call.Value.RoomName, token.Value.RoomName);
        Assert.True(token.Value.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Answer_SetsInProgressAndAnsweredAt()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var notifier = new RecordingCallNotifier();
        var service = NewService(db, new FakeCallProvider(), notifier);
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));

        var answered = await service.AnswerAsync(bob.Id, call.Value!.Id);

        Assert.True(answered.Succeeded);
        Assert.Equal("InProgress", answered.Value!.Status);
        Assert.NotNull(answered.Value.AnsweredAt);
        Assert.Contains(answered.Value.Participants, p => p.UserId == bob.Id && p.Status == "Joined");
        Assert.NotEmpty(notifier.Updated);
    }

    [Fact]
    public async Task End_SetsCompletedAndWritesCallMessage()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var provider = new FakeCallProvider();
        var notifier = new RecordingCallNotifier();
        var service = NewService(db, provider, notifier);
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));
        await service.AnswerAsync(bob.Id, call.Value!.Id);

        var ended = await service.EndAsync(alice.Id, call.Value.Id);

        Assert.True(ended.Succeeded);
        Assert.Equal("Completed", ended.Value!.Status);
        Assert.NotNull(ended.Value.EndedAt);
        Assert.NotNull(ended.Value.DurationSeconds);

        // Room phải được dọn (chống room treo) và có tin nhắn loại Call trong hội thoại.
        Assert.Contains(call.Value.RoomName, provider.EndedRooms);
        var message = db.Messages.Single(m => m.CallId == call.Value.Id);
        Assert.Equal(MessageType.Call, message.Type);
        Assert.StartsWith("call:Audio:", message.Content);
        Assert.NotEmpty(notifier.Ended);
    }

    [Fact]
    public async Task End_Twice_IsIdempotent()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var service = NewService(db, new FakeCallProvider(), new RecordingCallNotifier());
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));

        var first = await service.EndAsync(alice.Id, call.Value!.Id);
        var second = await service.EndAsync(bob.Id, call.Value.Id);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Value!.Status, second.Value!.Status);
        Assert.Equal(1, db.Messages.Count(m => m.CallId == call.Value.Id));
    }

    [Fact]
    public async Task ProviderRoomFinished_EndsCallAndCleansRoom()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var provider = new FakeCallProvider();
        var service = NewService(db, provider, new RecordingCallNotifier());
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));
        await service.AnswerAsync(bob.Id, call.Value!.Id);

        // Webhook báo room đã đóng (nguồn sự thật cuối cùng).
        await service.HandleProviderEventAsync(
            new CallProviderEvent(CallProviderEventType.RoomFinished, call.Value.RoomName));

        var stored = await db.Calls.FindAsync(call.Value.Id);
        Assert.Equal(CallStatus.Completed, stored!.Status);
        Assert.NotNull(stored.EndedAt);
        Assert.Contains(call.Value.RoomName, provider.EndedRooms);
    }

    [Fact]
    public async Task ProviderParticipantJoined_UpdatesParticipantStatus()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var service = NewService(db, new FakeCallProvider(), new RecordingCallNotifier());
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));

        await service.HandleProviderEventAsync(
            new CallProviderEvent(CallProviderEventType.ParticipantJoined, call.Value!.RoomName, bob.Id.ToString()));

        var participant = db.CallParticipants.Single(p => p.CallId == call.Value.Id && p.UserId == bob.Id);
        Assert.Equal(CallParticipantStatus.Joined, participant.Status);
        Assert.NotNull(participant.JoinedAt);
    }

    [Fact]
    public async Task GetHistory_ReturnsMyCallsOnly()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();
        var service = NewService(db, new FakeCallProvider(), new RecordingCallNotifier());
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));

        var aliceHistory = await service.GetHistoryAsync(alice.Id, null, 20);
        var outsiderHistory = await service.GetHistoryAsync(outsider.Id, null, 20);

        Assert.True(aliceHistory.Succeeded);
        Assert.Single(aliceHistory.Value!.Items);
        Assert.Equal(call.Value!.Id, aliceHistory.Value.Items[0].Id);
        Assert.True(aliceHistory.Value.Items[0].IWasInitiator);

        Assert.True(outsiderHistory.Succeeded);
        Assert.Empty(outsiderHistory.Value!.Items);
    }

    /// <summary>Cuộc gọi đổ chuông quá 30s mà không ai nghe phải được đóng thành Missed/NoAnswer.</summary>
    [Fact]
    public async Task TimeoutRingingCalls_EndsMissedAndNotifiesMembers()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var notifier = new RecordingCallNotifier();
        var service = NewService(db, new FakeCallProvider(), notifier);
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));

        // Đẩy cuộc gọi về quá khứ hơn 30 giây (CallRules.RingTimeoutSeconds).
        var stored = await db.Calls.FindAsync(call.Value!.Id);
        stored!.CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-31);
        await db.SaveChangesAsync();

        var finished = await service.TimeoutRingingCallsAsync();

        Assert.Equal(1, finished);

        var reloaded = await db.Calls.FindAsync(call.Value.Id);
        Assert.Equal(CallStatus.Missed, reloaded!.Status);
        Assert.Equal(CallEndReason.NoAnswer, reloaded.EndReason);
        Assert.NotNull(reloaded.EndedAt);

        var participant = db.CallParticipants.Single(p => p.CallId == call.Value.Id && p.UserId == bob.Id);
        Assert.Equal(CallParticipantStatus.Missed, participant.Status);

        // Có tin nhắn "Call" ghi lại cuộc gọi nhỡ và có phát CallEnded.
        Assert.Contains(db.Messages, m => m.CallId == call.Value.Id);
        Assert.NotEmpty(notifier.Ended);
    }

    /// <summary>Người nhận bấm nghe sau khi hết giờ đổ chuông → CALL_ALREADY_ENDED, không vào room.</summary>
    [Fact]
    public async Task Answer_AfterRingTimeout_IsRejected()
    {
        var (db, alice, bob, conversation) = await CreateDirectPairAsync();
        var service = NewService(db, new FakeCallProvider(), new RecordingCallNotifier());
        var call = await service.StartAsync(alice.Id, new StartCallRequest(conversation.Id, "Audio"));

        var stored = await db.Calls.FindAsync(call.Value!.Id);
        stored!.CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-31);
        await db.SaveChangesAsync();

        var answered = await service.AnswerAsync(bob.Id, call.Value.Id);

        Assert.False(answered.Succeeded);
        Assert.Equal(ErrorCodes.CallAlreadyEnded, answered.ErrorCode);

        var reloaded = await db.Calls.FindAsync(call.Value.Id);
        Assert.Equal(CallStatus.Missed, reloaded!.Status);
    }
}
