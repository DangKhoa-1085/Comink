// Test tích hợp cho mốc "đã đọc" (NC Giai đoạn 7 — docs/tasks/phase7_advanced_messaging.md mục 4.9):
// quyền truy cập, chỉ tiến mốc, phát sự kiện và số tin chưa đọc.
using Comink.Application.Common;
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class ReadReceiptTests : IntegrationTestBase
{
    public ReadReceiptTests(PostgresFixture fixture) : base(fixture)
    {
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

    private static ConversationService Conversations(ApplicationDbContext db, RecordingChatNotifier notifier) =>
        new(db, new FakePresenceService(), notifier);

    private async Task<(ApplicationDbContext Db, ApplicationUser Alice, ApplicationUser Bob)> CreatePairAsync()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice", "Alice");
        var bob = NewUser("bob", "Bob");
        db.Users.AddRange(alice, bob);
        db.Friendships.Add(new Friendship
        {
            RequesterId = alice.Id,
            AddresseeId = bob.Id,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return (db, alice, bob);
    }

    [Fact]
    public async Task MarkRead_AdvancesMarkerAndNotifiesEveryMember()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var conversations = Conversations(db, notifier);
        var messages = new MessageService(db, new NullChatNotifier());

        var conversationId = (await conversations.OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        await messages.SendAsync(alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin 1", null));
        var newest = await messages.SendAsync(
            bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin 2", null));

        var result = await conversations.MarkReadAsync(alice.Id, conversationId, newest.Value!.Message.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(newest.Value.Message.Id, result.Value!.MessageId);

        var member = await db.ConversationMembers.FindAsync(conversationId, alice.Id);
        Assert.Equal(newest.Value.Message.Id, member!.LastReadMessageId);
        Assert.NotNull(member.LastReadAt);

        // Phải phát cho CẢ hai thành viên — người gửi (Bob) cần thấy "Đã đọc" ngay.
        var read = Assert.Single(notifier.Reads);
        Assert.Equal(conversationId, read.ConversationId);
        Assert.Equal(alice.Id, read.Receipt.UserId);
        Assert.Equal(2, read.Members.Count);
        Assert.Contains(alice.Id, read.Members);
        Assert.Contains(bob.Id, read.Members);
    }

    [Fact]
    public async Task MarkRead_WithOlderMessage_DoesNotMoveMarkerOrNotifyAgain()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var conversations = Conversations(db, notifier);
        var messages = new MessageService(db, new NullChatNotifier());

        var conversationId = (await conversations.OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        var first = await messages.SendAsync(
            bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin 1", null));
        var second = await messages.SendAsync(
            bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin 2", null));

        await conversations.MarkReadAsync(alice.Id, conversationId, second.Value!.Message.Id);

        // Tab cũ gửi mốc của tin cũ hơn: vẫn 204 (idempotent) nhưng KHÔNG được lùi mốc.
        var older = await conversations.MarkReadAsync(alice.Id, conversationId, first.Value!.Message.Id);

        Assert.True(older.Succeeded);
        Assert.Single(notifier.Reads);

        var member = await db.ConversationMembers.FindAsync(conversationId, alice.Id);
        Assert.Equal(second.Value.Message.Id, member!.LastReadMessageId);
        Assert.NotEqual(first.Value.Message.Id, member.LastReadMessageId);
    }

    [Fact]
    public async Task MarkRead_WithoutAdvancing_ReturnsCurrentMarkerWithoutEvent()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var conversations = Conversations(db, notifier);
        var messages = new MessageService(db, new NullChatNotifier());

        var conversationId = (await conversations.OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        var only = await messages.SendAsync(
            bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin 1", null));

        await conversations.MarkReadAsync(alice.Id, conversationId, only.Value!.Message.Id);
        var again = await conversations.MarkReadAsync(alice.Id, conversationId, only.Value.Message.Id);

        Assert.True(again.Succeeded);
        Assert.Equal(only.Value.Message.Id, again.Value!.MessageId);
        Assert.Single(notifier.Reads);
    }

    [Fact]
    public async Task MarkRead_ByNonMember_IsNotFound()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var conversations = Conversations(db, new RecordingChatNotifier());
        var messages = new MessageService(db, new NullChatNotifier());
        var conversationId = (await conversations.OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        var message = await messages.SendAsync(
            bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin 1", null));

        var result = await conversations.MarkReadAsync(
            outsider.Id, conversationId, message.Value!.Message.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConversationNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MarkRead_WithMessageFromAnotherConversation_IsNotFound()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var carol = NewUser("carol", "Carol");
        db.Users.Add(carol);
        db.Friendships.Add(new Friendship
        {
            RequesterId = bob.Id,
            AddresseeId = carol.Id,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var notifier = new RecordingChatNotifier();
        var conversations = Conversations(db, notifier);
        var messages = new MessageService(db, new NullChatNotifier());

        var aliceBob = (await conversations.OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        var bobCarol = (await conversations.OpenDirectAsync(bob.Id, carol.Id)).Value!.Conversation.Id;
        var foreignMessage = await messages.SendAsync(
            carol.Id, bobCarol, new SendMessageRequest(Guid.CreateVersion7(), "tin riêng", null));

        var result = await conversations.MarkReadAsync(
            alice.Id, aliceBob, foreignMessage.Value!.Message.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageNotFound, result.ErrorCode);
        Assert.Empty(notifier.Reads);
    }

    [Fact]
    public async Task UnreadCount_CountsOnlyMessagesFromOthersAfterMarker()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var conversations = Conversations(db, new RecordingChatNotifier());
        var messages = new MessageService(db, new NullChatNotifier());

        var conversationId = (await conversations.OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        await messages.SendAsync(alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tôi gửi", null));
        await messages.SendAsync(bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "bob 1", null));
        var bobLast = await messages.SendAsync(
            bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "bob 2", null));

        var aliceList = await conversations.GetMyConversationsAsync(alice.Id, null, 20);
        var aliceSummary = Assert.Single(aliceList.Value!.Items, item => item.Id == conversationId);
        // Lấy đúng phép đếm chưa đọc: chỉ tin của Bob.
        Assert.Equal(2, aliceSummary.UnreadCount);

        var bobList = await conversations.GetMyConversationsAsync(bob.Id, null, 20);
        var bobSummary = Assert.Single(bobList.Value!.Items, item => item.Id == conversationId);
        Assert.Equal(1, bobSummary.UnreadCount);

        await conversations.MarkReadAsync(alice.Id, conversationId, bobLast.Value!.Message.Id);

        var after = await conversations.GetMyConversationsAsync(alice.Id, null, 20);
        var afterSummary = Assert.Single(after.Value!.Items, item => item.Id == conversationId);
        Assert.Equal(0, afterSummary.UnreadCount);
    }
}
