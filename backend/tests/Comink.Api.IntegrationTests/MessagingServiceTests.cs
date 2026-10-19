// Test tích hợp cho nhắn tin: mở hội thoại 1–1, gửi tin idempotent, lịch sử và quyền truy cập
// (docs/05_api_contract.md mục 8, docs/tasks/phase3_messaging.md mục 4.8).
using Comink.Application.Common;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class MessagingServiceTests : IntegrationTestBase
{
    public MessagingServiceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    /// <summary>Không phát sóng gì — test nghiệp vụ không cần SignalR.</summary>
    private sealed class NullChatNotifier : IChatNotifier
    {
        public Task MessageCreatedAsync(
            Guid conversationId,
            IReadOnlyList<Guid> memberUserIds,
            MessageDto message,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Ghi lại lần phát sự kiện để khẳng định danh sách người nhận.</summary>
    private sealed class RecordingChatNotifier : IChatNotifier
    {
        public List<(Guid ConversationId, IReadOnlyList<Guid> Members, Guid MessageId)> Calls { get; } = [];

        public Task MessageCreatedAsync(
            Guid conversationId,
            IReadOnlyList<Guid> memberUserIds,
            MessageDto message,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((conversationId, memberUserIds, message.Id));
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

    private async Task<(ApplicationDbContext Db, ApplicationUser First, ApplicationUser Second)> CreatePairAsync(
        bool areFriends = true)
    {
        var db = await CreateDbContextAsync();
        var first = NewUser("alice", "Alice");
        var second = NewUser("bob", "Bob");
        db.Users.AddRange(first, second);

        if (areFriends)
        {
            db.Friendships.Add(new Friendship
            {
                RequesterId = first.Id,
                AddresseeId = second.Id,
                Status = FriendshipStatus.Accepted,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return (db, first, second);
    }

    private static ConversationService Conversations(ApplicationDbContext db) => new(db);

    private static MessageService Messages(ApplicationDbContext db) => new(db, new NullChatNotifier());

    [Fact]
    public async Task OpenDirect_WhenFriends_CreatesConversationWithTwoMembers()
    {
        var (db, alice, bob) = await CreatePairAsync();

        var result = await Conversations(db).OpenDirectAsync(alice.Id, bob.Id);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.WasCreated);
        Assert.Equal(2, result.Value.Conversation.Members.Count);
        Assert.Equal(nameof(ConversationType.Direct), result.Value.Conversation.Type);
    }

    [Fact]
    public async Task OpenDirect_SecondTime_ReturnsSameConversationForBothSides()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var service = Conversations(db);

        var created = await service.OpenDirectAsync(alice.Id, bob.Id);
        var againByAlice = await service.OpenDirectAsync(alice.Id, bob.Id);
        var againByBob = await service.OpenDirectAsync(bob.Id, alice.Id);

        Assert.True(againByAlice.Succeeded);
        Assert.False(againByAlice.Value!.WasCreated);
        Assert.Equal(created.Value!.Conversation.Id, againByAlice.Value.Conversation.Id);

        // Khoá direct_key phải đối xứng: phía kia mở cũng ra đúng hội thoại đó.
        Assert.Equal(created.Value.Conversation.Id, againByBob.Value!.Conversation.Id);
    }

    [Fact]
    public async Task OpenDirect_WhenNotFriends_IsForbidden()
    {
        var (db, alice, bob) = await CreatePairAsync(areFriends: false);

        var result = await Conversations(db).OpenDirectAsync(alice.Id, bob.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessagingNotFriends, result.ErrorCode);
    }

    [Fact]
    public async Task OpenDirect_WithSelf_IsRejected()
    {
        var (db, alice, _) = await CreatePairAsync();

        var result = await Conversations(db).OpenDirectAsync(alice.Id, alice.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessagingCannotMessageSelf, result.ErrorCode);
    }

    [Fact]
    public async Task Send_SavesMessageAndUpdatesConversationPointer()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var conversation = await Conversations(db).OpenDirectAsync(alice.Id, bob.Id);
        var conversationId = conversation.Value!.Conversation.Id;

        var result = await Messages(db).SendAsync(
            alice.Id,
            conversationId,
            new SendMessageRequest(Guid.CreateVersion7(), "Xin chào Bob", null));

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.WasCreated);
        Assert.Equal("Xin chào Bob", result.Value.Message.Content);
        Assert.Equal(nameof(MessageType.Text), result.Value.Message.Type);

        var stored = await db.Conversations.FindAsync(conversationId);
        Assert.Equal(result.Value.Message.Id, stored!.LastMessageId);
        Assert.NotNull(stored.LastMessageAt);
    }

    [Fact]
    public async Task Send_SameClientMessageId_ReturnsExistingMessageOnce()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var conversationId = (await Conversations(db).OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        var clientMessageId = Guid.CreateVersion7();

        var first = await Messages(db).SendAsync(
            alice.Id, conversationId, new SendMessageRequest(clientMessageId, "Chỉ một lần", null));
        var second = await Messages(db).SendAsync(
            alice.Id, conversationId, new SendMessageRequest(clientMessageId, "Chỉ một lần", null));

        Assert.True(first.Value!.WasCreated);
        Assert.False(second.Value!.WasCreated);
        Assert.Equal(first.Value.Message.Id, second.Value.Message.Id);

        var count = db.Messages.Count(m => m.ConversationId == conversationId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Send_WithoutContentOrMedia_IsRejected()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var conversationId = (await Conversations(db).OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;

        var result = await Messages(db).SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "   ", null));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageContentRequired, result.ErrorCode);
    }

    [Fact]
    public async Task Send_ByNonMember_IsRejectedAsNotFound()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var conversationId = (await Conversations(db).OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;

        var result = await Messages(db).SendAsync(
            outsider.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "Tôi chen vào", null));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConversationNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task History_ForNonMember_IsRejectedAsNotFound()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();
        var conversationId = (await Conversations(db).OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;

        var result = await Messages(db).GetHistoryAsync(outsider.Id, conversationId, null, null, 20);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConversationNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task History_ReturnsNewestFirst_AndPagesBackwards()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var conversationId = (await Conversations(db).OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        var messages = Messages(db);

        for (var index = 1; index <= 3; index++)
        {
            await messages.SendAsync(
                alice.Id,
                conversationId,
                new SendMessageRequest(Guid.CreateVersion7(), $"tin {index}", null));
        }

        var firstPage = await messages.GetHistoryAsync(alice.Id, conversationId, null, null, 2);

        Assert.True(firstPage.Succeeded);
        Assert.Equal(2, firstPage.Value!.Items.Count);
        Assert.True(firstPage.Value.HasMore);
        // Mới nhất trước (cuộn lên tải thêm).
        Assert.Equal("tin 3", firstPage.Value.Items[0].Content);

        var secondPage = await messages.GetHistoryAsync(
            alice.Id, conversationId, firstPage.Value.NextCursor, null, 2);

        Assert.True(secondPage.Succeeded);
        Assert.Single(secondPage.Value!.Items);
        Assert.Equal("tin 1", secondPage.Value.Items[0].Content);
    }

    [Fact]
    public async Task History_AfterCursor_ReturnsOnlyNewerMessages()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var conversationId = (await Conversations(db).OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        var messages = Messages(db);

        var first = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin 1", null));

        var cursorCreatedAt = first.Value!.Message.CreatedAt;
        var cursorId = first.Value.Message.Id;
        var cursor = CursorCodec.Encode(cursorCreatedAt, cursorId);

        // Đồng bộ sau khi mất kết nối: chỉ lấy tin mới hơn mốc, cũ nhất trước.
        await messages.SendAsync(
            bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin 2", null));
        await messages.SendAsync(
            bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin 3", null));

        var result = await messages.GetHistoryAsync(alice.Id, conversationId, null, cursor, 20);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.Equal("tin 2", result.Value.Items[0].Content);
        Assert.Equal("tin 3", result.Value.Items[1].Content);
    }

    [Fact]
    public async Task History_WithBothCursors_IsRejected()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var conversationId = (await Conversations(db).OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;

        var result = await Messages(db).GetHistoryAsync(alice.Id, conversationId, "x", "y", 20);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Send_NotifiesEveryMemberOfTheConversation()
    {
        // Lỗi thật đã gặp khi kiểm thử GĐ 3: chỉ phát vào group hội thoại, mà client chỉ vào group
        // lúc kết nối → hội thoại tạo sau đó thì phải tải lại trang mới thấy tin.
        // Nay phải phát cho TẤT CẢ thành viên (group user:{id} luôn có sẵn).
        var (db, alice, bob) = await CreatePairAsync();
        var conversationId = (await Conversations(db).OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        var notifier = new RecordingChatNotifier();
        var service = new MessageService(db, notifier);

        var sent = await service.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "xin chào", null));

        var call = Assert.Single(notifier.Calls);
        Assert.Equal(conversationId, call.ConversationId);
        Assert.Equal(sent.Value!.Message.Id, call.MessageId);
        Assert.Equal(2, call.Members.Count);
        Assert.Contains(alice.Id, call.Members);
        Assert.Contains(bob.Id, call.Members);
    }

    [Fact]
    public async Task GetActiveConversationIds_OnlyReturnsMemberships()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var conversationId = (await Conversations(db).OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;

        var aliceIds = await Conversations(db).GetActiveConversationIdsAsync(alice.Id);
        var outsiderIds = await Conversations(db).GetActiveConversationIdsAsync(Guid.CreateVersion7());

        Assert.Contains(conversationId, aliceIds);
        Assert.Empty(outsiderIds);
    }
}
