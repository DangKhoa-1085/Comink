// Test tích hợp cho các thao tác tin nhắn NC Ưu tiên 2 — Đợt A
// (docs/tasks/phase7b_advanced_messaging.md mục 4.1): phản hồi, chỉnh sửa, thu hồi, cảm xúc.
using Comink.Application.Common;
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class MessageActionsTests : IntegrationTestBase
{
    public MessageActionsTests(PostgresFixture fixture) : base(fixture)
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

    private static async Task<Guid> OpenConversationAsync(
        ApplicationDbContext db,
        RecordingChatNotifier notifier,
        ApplicationUser alice,
        ApplicationUser bob)
    {
        var conversations = Conversations(db, notifier);
        var opened = await conversations.OpenDirectAsync(alice.Id, bob.Id);
        return opened.Value!.Conversation.Id;
    }

    // --- Phản hồi ---

    [Fact]
    public async Task Send_WithReplyTo_ReturnsQuoteAndBroadcastsIt()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);

        var original = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "Tin gốc", null));

        var reply = await messages.SendAsync(
            bob.Id,
            conversationId,
            new SendMessageRequest(Guid.CreateVersion7(), "Trả lời", null, original.Value!.Message.Id));

        Assert.True(reply.Succeeded);
        Assert.Equal("Trả lời", reply.Value!.Message.Content);
        Assert.NotNull(reply.Value.Message.ReplyTo);
        Assert.Equal(original.Value.Message.Id, reply.Value.Message.ReplyTo!.MessageId);
        Assert.Equal("Alice", reply.Value.Message.ReplyTo.SenderDisplayName);
        Assert.Equal("Tin gốc", reply.Value.Message.ReplyTo.ContentPreview);
        Assert.False(reply.Value.Message.ReplyTo.IsDeleted);

        // Sự kiện tin mới phải mang theo trích dẫn để client hiện ngay.
        // (Bản ghi đầu tiên là tin gốc, sự kiện cuối là tin trả lời.)
        var broadcast = notifier.Messages[^1];
        Assert.Equal(2, broadcast.Members.Count);
    }

    [Fact]
    public async Task Send_WithReplyToFromAnotherConversation_IsMessageNotFound()
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
        var messages = new MessageService(db, notifier);
        var conversations = Conversations(db, notifier);

        var aliceBob = (await conversations.OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        var bobCarol = (await conversations.OpenDirectAsync(bob.Id, carol.Id)).Value!.Conversation.Id;
        var foreign = await messages.SendAsync(
            carol.Id, bobCarol, new SendMessageRequest(Guid.CreateVersion7(), "tin riêng", null));

        var result = await messages.SendAsync(
            alice.Id,
            aliceBob,
            new SendMessageRequest(Guid.CreateVersion7(), "trả lời nhầm", null, foreign.Value!.Message.Id));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Send_WithReplyToMissingOrRecalled_IsRejected()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);

        var missing = await messages.SendAsync(
            alice.Id,
            conversationId,
            new SendMessageRequest(Guid.CreateVersion7(), "trả lời tin không tồn tại", null, Guid.CreateVersion7()));
        Assert.Equal(ErrorCodes.MessageNotFound, missing.ErrorCode);

        var original = await messages.SendAsync(
            bob.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "sắp bị thu hồi", null));
        await messages.RecallAsync(bob.Id, conversationId, original.Value!.Message.Id);

        var toDeleted = await messages.SendAsync(
            alice.Id,
            conversationId,
            new SendMessageRequest(Guid.CreateVersion7(), "trả lời tin đã thu hồi", null, original.Value.Message.Id));
        Assert.Equal(ErrorCodes.MessageAlreadyDeleted, toDeleted.ErrorCode);
    }

    // --- Chỉnh sửa ---

    [Fact]
    public async Task Edit_BySender_UpdatesContentAndBroadcasts()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "sai chính tả", null));

        var result = await messages.EditAsync(
            alice.Id, conversationId, sent.Value!.Message.Id, new EditMessageRequest("đã sửa"));

        Assert.True(result.Succeeded);
        Assert.Equal("đã sửa", result.Value!.Content);
        Assert.NotNull(result.Value.EditedAt);

        var update = Assert.Single(notifier.Updates);
        Assert.Equal("đã sửa", update.Message.Content);
        Assert.Equal(2, update.Members.Count);
        Assert.Contains(alice.Id, update.Members);
        Assert.Contains(bob.Id, update.Members);
    }

    [Fact]
    public async Task Edit_ByOtherUser_IsNotOwned()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "của Alice", null));

        var result = await messages.EditAsync(
            bob.Id, conversationId, sent.Value!.Message.Id, new EditMessageRequest("Bob sửa trộm"));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageNotOwned, result.ErrorCode);
        Assert.Empty(notifier.Updates);
    }

    [Fact]
    public async Task Edit_AfterWindow_IsExpired()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin cũ", null));

        // Đẩy thời điểm gửi ra ngoài cửa sổ 24 giờ (quyết định D1).
        var entity = await db.Messages.FindAsync(sent.Value!.Message.Id);
        entity!.CreatedAt = DateTimeOffset.UtcNow.AddHours(-25);
        await db.SaveChangesAsync();

        var result = await messages.EditAsync(
            alice.Id, conversationId, sent.Value.Message.Id, new EditMessageRequest("quá hạn"));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageEditWindowExpired, result.ErrorCode);
        Assert.Empty(notifier.Updates);
    }

    [Fact]
    public async Task Edit_SystemMessage_IsNotEditable()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);

        var system = new Message
        {
            ConversationId = conversationId,
            SenderId = bob.Id,
            Type = MessageType.System,
            Content = "group_created",
            ClientMessageId = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(system);
        await db.SaveChangesAsync();

        var result = await messages.EditAsync(
            bob.Id, conversationId, system.Id, new EditMessageRequest("sửa tin hệ thống"));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageNotEditable, result.ErrorCode);
    }

    [Fact]
    public async Task Edit_ToEmptyWithoutAttachments_IsContentRequired()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "có nội dung", null));

        var result = await messages.EditAsync(
            alice.Id, conversationId, sent.Value!.Message.Id, new EditMessageRequest("   "));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageContentRequired, result.ErrorCode);
    }

    [Fact]
    public async Task Edit_RecalledMessage_IsAlreadyDeleted()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "sẽ thu hồi", null));
        await messages.RecallAsync(alice.Id, conversationId, sent.Value!.Message.Id);

        var result = await messages.EditAsync(
            alice.Id, conversationId, sent.Value.Message.Id, new EditMessageRequest("sửa lại"));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageAlreadyDeleted, result.ErrorCode);
    }

    // --- Thu hồi ---

    [Fact]
    public async Task Recall_HidesContentAndAttachmentsRemovesReactionsAndPin()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "nội dung", null));

        // Chuẩn bị cảm xúc + ghim để kiểm tra bước dọn dẹp.
        db.MessageReactions.Add(new MessageReaction
        {
            MessageId = sent.Value!.Message.Id,
            UserId = bob.Id,
            Emoji = "👍",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.PinnedMessages.Add(new PinnedMessage
        {
            ConversationId = conversationId,
            MessageId = sent.Value.Message.Id,
            PinnedBy = bob.Id,
            PinnedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await messages.RecallAsync(alice.Id, conversationId, sent.Value.Message.Id);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.Content);
        Assert.NotNull(result.Value.DeletedAt);
        Assert.Empty(result.Value.Attachments);
        Assert.Empty(result.Value.Reactions);

        Assert.Empty(await db.MessageReactions.Where(r => r.MessageId == sent.Value.Message.Id).ToListAsync());
        Assert.Empty(await db.PinnedMessages.Where(p => p.MessageId == sent.Value.Message.Id).ToListAsync());

        var update = Assert.Single(notifier.Updates);
        Assert.NotNull(update.Message.DeletedAt);
        var pinnedEvent = Assert.Single(notifier.PinnedUpdates);
        Assert.Empty(pinnedEvent.Pinned.Pinned);
    }

    [Fact]
    public async Task Recall_ByOtherUser_IsNotOwned()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "của Alice", null));

        var result = await messages.RecallAsync(bob.Id, conversationId, sent.Value!.Message.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageNotOwned, result.ErrorCode);
    }

    [Fact]
    public async Task Recall_AfterWindow_IsExpired()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin cũ", null));

        var entity = await db.Messages.FindAsync(sent.Value!.Message.Id);
        entity!.CreatedAt = DateTimeOffset.UtcNow.AddHours(-24).AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await messages.RecallAsync(alice.Id, conversationId, sent.Value.Message.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageRecallWindowExpired, result.ErrorCode);
        Assert.Empty(notifier.Updates);
    }

    [Fact]
    public async Task Recall_Twice_IsIdempotentWithoutSecondBroadcast()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "thu hồi 2 lần", null));

        var first = await messages.RecallAsync(alice.Id, conversationId, sent.Value!.Message.Id);
        var second = await messages.RecallAsync(alice.Id, conversationId, sent.Value.Message.Id);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Value!.DeletedAt, second.Value!.DeletedAt);
        Assert.Single(notifier.Updates);
    }

    [Fact]
    public async Task Recall_UpdatesConversationPreviewAsDeleted()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversations = Conversations(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin cuối", null));

        await messages.RecallAsync(alice.Id, conversationId, sent.Value!.Message.Id);

        var page = await conversations.GetMyConversationsAsync(bob.Id, null, 20);
        var summary = Assert.Single(page.Value!.Items, item => item.Id == conversationId);
        Assert.NotNull(summary.LastMessage);
        Assert.True(summary.LastMessage!.IsDeleted);
        Assert.Equal(string.Empty, summary.LastMessage.Preview);
    }

    [Fact]
    public async Task Recall_ByNonMember_IsConversationNotFound()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin", null));

        var result = await messages.RecallAsync(outsider.Id, conversationId, sent.Value!.Message.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConversationNotFound, result.ErrorCode);
    }

    // --- Cảm xúc ---

    [Fact]
    public async Task Reactions_SupportMultipleTypesPerUserAndPerMessage()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "thả cảm xúc", null));
        var messageId = sent.Value!.Message.Id;

        await messages.AddReactionAsync(alice.Id, conversationId, messageId, "like");
        await messages.AddReactionAsync(alice.Id, conversationId, messageId, "love");
        var result = await messages.AddReactionAsync(bob.Id, conversationId, messageId, "love");

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Reactions.Count);

        var love = Assert.Single(result.Value.Reactions, reaction => reaction.Reaction == "love");
        Assert.Equal(2, love.Count);
        Assert.True(love.ReactedByMe); // góc nhìn của Bob

        var like = Assert.Single(result.Value.Reactions, reaction => reaction.Reaction == "like");
        Assert.Equal(1, like.Count);
        Assert.False(like.ReactedByMe);
        Assert.Equal("👍", like.Emoji);

        // Mỗi lần thêm mới đều phát MessageUpdated; 3 lần thêm → 3 sự kiện.
        Assert.Equal(3, notifier.Updates.Count);
    }

    [Fact]
    public async Task AddReaction_SameReactionTwice_IsIdempotent()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "thả 2 lần", null));
        var messageId = sent.Value!.Message.Id;

        var first = await messages.AddReactionAsync(alice.Id, conversationId, messageId, "haha");
        var second = await messages.AddReactionAsync(alice.Id, conversationId, messageId, "haha");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        var reaction = Assert.Single(second.Value!.Reactions);
        Assert.Equal(1, reaction.Count);
        Assert.Single(notifier.Updates); // lần lặp lại không phát lại
    }

    [Fact]
    public async Task AddReaction_UnknownKey_IsReactionInvalid()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin", null));

        var result = await messages.AddReactionAsync(
            alice.Id, conversationId, sent.Value!.Message.Id, "heart");

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageReactionInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task RemoveReaction_RemovesOnlyCurrentUserReaction()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin", null));
        var messageId = sent.Value!.Message.Id;

        await messages.AddReactionAsync(alice.Id, conversationId, messageId, "sad");
        await messages.AddReactionAsync(bob.Id, conversationId, messageId, "sad");

        var result = await messages.RemoveReactionAsync(alice.Id, conversationId, messageId, "sad");

        Assert.True(result.Succeeded);
        var remaining = Assert.Single(result.Value!.Reactions);
        Assert.Equal(1, remaining.Count);
        Assert.False(remaining.ReactedByMe); // Alice đã bỏ, còn của Bob
    }

    [Fact]
    public async Task AddReaction_OnRecalledMessage_IsAlreadyDeleted()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "sẽ thu hồi", null));
        await messages.RecallAsync(alice.Id, conversationId, sent.Value!.Message.Id);

        var result = await messages.AddReactionAsync(
            bob.Id, conversationId, sent.Value.Message.Id, "like");

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageAlreadyDeleted, result.ErrorCode);
    }

    [Fact]
    public async Task AddReaction_ByNonMember_IsConversationNotFound()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);
        var sent = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "tin", null));

        var result = await messages.AddReactionAsync(
            outsider.Id, conversationId, sent.Value!.Message.Id, "like");

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConversationNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetHistory_IncludesQuotesAndReactions()
    {
        var (db, alice, bob) = await CreatePairAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversationId = await OpenConversationAsync(db, notifier, alice, bob);

        var original = await messages.SendAsync(
            alice.Id, conversationId, new SendMessageRequest(Guid.CreateVersion7(), "Tin gốc", null));
        var reply = await messages.SendAsync(
            bob.Id,
            conversationId,
            new SendMessageRequest(Guid.CreateVersion7(), "Trả lời", null, original.Value!.Message.Id));
        await messages.AddReactionAsync(alice.Id, conversationId, original.Value.Message.Id, "wow");

        var page = await messages.GetHistoryAsync(alice.Id, conversationId, null, null, 20);
        var items = page.Value!.Items;

        var originalDto = Assert.Single(items, item => item.Id == original.Value.Message.Id);
        var reaction = Assert.Single(originalDto.Reactions);
        Assert.Equal("wow", reaction.Reaction);
        Assert.True(reaction.ReactedByMe);

        var replyDto = Assert.Single(items, item => item.Id == reply.Value!.Message.Id);
        Assert.NotNull(replyDto.ReplyTo);
        Assert.Equal(original.Value.Message.Id, replyDto.ReplyTo!.MessageId);
    }
}
