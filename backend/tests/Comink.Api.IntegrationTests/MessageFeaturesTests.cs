// Test tích hợp cho Đợt B (docs/tasks/phase7b_advanced_messaging.md mục 4.2):
// ghim, chuyển tiếp, tìm kiếm/ngữ cảnh, tắt thông báo.
using Comink.Application.Common;
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class MessageFeaturesTests : IntegrationTestBase
{
    public MessageFeaturesTests(PostgresFixture fixture) : base(fixture)
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

    private static async Task BefriendAsync(ApplicationDbContext db, ApplicationUser first, ApplicationUser second)
    {
        db.Friendships.Add(new Friendship
        {
            RequesterId = first.Id,
            AddresseeId = second.Id,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<(ApplicationDbContext Db, ApplicationUser Alice, ApplicationUser Bob, Guid ConversationId)>
        CreateConversationAsync()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice", "Alice");
        var bob = NewUser("bob", "Bob");
        db.Users.AddRange(alice, bob);
        await BefriendAsync(db, alice, bob);

        var notifier = new RecordingChatNotifier();
        var conversations = Conversations(db, notifier);
        var conversationId = (await conversations.OpenDirectAsync(alice.Id, bob.Id)).Value!.Conversation.Id;
        return (db, alice, bob, conversationId);
    }

    private static async Task<MessageDto> SendAsync(
        MessageService messages,
        Guid senderId,
        Guid conversationId,
        string content,
        Guid? replyTo = null)
    {
        var sent = await messages.SendAsync(
            senderId,
            conversationId,
            new SendMessageRequest(Guid.CreateVersion7(), content, null, replyTo));
        Assert.True(sent.Succeeded);
        return sent.Value!.Message;
    }

    // --- Ghim ---

    [Fact]
    public async Task Pin_AddsMessageToListWithPinnerNameAndBroadcasts()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var pins = new PinnedMessageService(db, notifier);
        var message = await SendAsync(messages, alice.Id, conversationId, "tin cần ghim");

        var result = await pins.PinAsync(bob.Id, conversationId, message.Id);

        Assert.True(result.Succeeded);
        var pinned = Assert.Single(result.Value!.Pinned);
        Assert.Equal(message.Id, pinned.Message.Id);
        Assert.Equal("Bob", pinned.PinnedByDisplayName);
        Assert.Equal(bob.Id, pinned.PinnedByUserId);

        var broadcast = Assert.Single(notifier.PinnedUpdates);
        Assert.Equal(conversationId, broadcast.ConversationId);
        Assert.Contains(alice.Id, broadcast.Members);
        Assert.Contains(bob.Id, broadcast.Members);
    }

    [Fact]
    public async Task Pin_IsIdempotentWithoutSecondBroadcast()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var pins = new PinnedMessageService(db, notifier);
        var message = await SendAsync(messages, alice.Id, conversationId, "ghim 2 lần");

        await pins.PinAsync(bob.Id, conversationId, message.Id);
        var second = await pins.PinAsync(bob.Id, conversationId, message.Id);

        Assert.True(second.Succeeded);
        Assert.Single(second.Value!.Pinned);
        Assert.Single(notifier.PinnedUpdates);
    }

    [Fact]
    public async Task Pin_EleventhMessage_IsRejected()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var pins = new PinnedMessageService(db, notifier);

        var ids = new List<Guid>();
        for (var index = 0; index < 11; index++)
        {
            ids.Add((await SendAsync(messages, alice.Id, conversationId, $"tin {index}")).Id);
        }

        for (var index = 0; index < 10; index++)
        {
            var pinned = await pins.PinAsync(alice.Id, conversationId, ids[index]);
            Assert.True(pinned.Succeeded);
        }

        var overLimit = await pins.PinAsync(alice.Id, conversationId, ids[10]);

        Assert.False(overLimit.Succeeded);
        Assert.Equal(ErrorCodes.MessagePinLimitReached, overLimit.ErrorCode);
        Assert.Equal(10, (await pins.GetAsync(alice.Id, conversationId)).Value!.Pinned.Count);
    }

    [Fact]
    public async Task Pin_RecalledMessage_IsAlreadyDeleted()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var pins = new PinnedMessageService(db, notifier);
        var message = await SendAsync(messages, alice.Id, conversationId, "sẽ thu hồi");
        await messages.RecallAsync(alice.Id, conversationId, message.Id);

        var result = await pins.PinAsync(bob.Id, conversationId, message.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageAlreadyDeleted, result.ErrorCode);
    }

    [Fact]
    public async Task Unpin_RemovesAndIdempotentWhenMissing()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var pins = new PinnedMessageService(db, notifier);
        var message = await SendAsync(messages, alice.Id, conversationId, "ghim rồi bỏ");

        await pins.PinAsync(alice.Id, conversationId, message.Id);
        var removed = await pins.UnpinAsync(alice.Id, conversationId, message.Id);
        var again = await pins.UnpinAsync(alice.Id, conversationId, message.Id);

        Assert.True(removed.Succeeded);
        Assert.Empty(removed.Value!.Pinned);
        Assert.True(again.Succeeded);
        Assert.Empty(again.Value!.Pinned);
        // Chỉ sự kiện ghim + bỏ ghim thật, lần bỏ thứ hai không phát.
        Assert.Equal(2, notifier.PinnedUpdates.Count);
    }

    [Fact]
    public async Task Pin_ByNonMember_IsConversationNotFound()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var pins = new PinnedMessageService(db, notifier);
        var message = await SendAsync(messages, alice.Id, conversationId, "tin");

        var result = await pins.PinAsync(outsider.Id, conversationId, message.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConversationNotFound, result.ErrorCode);
    }

    // --- Chuyển tiếp ---

    [Fact]
    public async Task Forward_CopiesContentAndLinkToSourceAndBroadcasts()
    {
        var (db, alice, bob, sourceConversationId) = await CreateConversationAsync();
        var carol = NewUser("carol", "Carol");
        db.Users.Add(carol);
        await BefriendAsync(db, alice, carol);

        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversations = Conversations(db, notifier);
        var targetConversationId = (await conversations.OpenDirectAsync(alice.Id, carol.Id)).Value!.Conversation.Id;

        var source = await SendAsync(messages, alice.Id, sourceConversationId, "Nội dung chuyển tiếp");
        notifier.Messages.Clear();

        var forwarded = await messages.ForwardAsync(
            alice.Id,
            targetConversationId,
            new ForwardMessageRequest(source.Id, Guid.CreateVersion7()));

        Assert.True(forwarded.Succeeded);
        Assert.True(forwarded.Value!.WasCreated);
        Assert.Equal("Nội dung chuyển tiếp", forwarded.Value.Message.Content);
        Assert.NotNull(forwarded.Value.Message.ForwardedFrom);
        Assert.Equal(source.Id, forwarded.Value.Message.ForwardedFrom!.MessageId);
        Assert.Equal(alice.Id, forwarded.Value.Message.ForwardedFrom.SenderId);

        var broadcast = Assert.Single(notifier.Messages);
        Assert.Equal(targetConversationId, broadcast.ConversationId);
        Assert.Contains(carol.Id, broadcast.Members);
        Assert.DoesNotContain(bob.Id, broadcast.Members);
    }

    [Fact]
    public async Task Forward_ByNonMemberOfSource_IsMessageNotFound()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var carol = NewUser("carol", "Carol");
        db.Users.Add(carol);
        await BefriendAsync(db, bob, carol);

        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversations = Conversations(db, notifier);
        var source = await SendAsync(messages, alice.Id, conversationId, "tin riêng");
        var carolConversation = (await conversations.OpenDirectAsync(carol.Id, bob.Id)).Value!.Conversation.Id;

        // Carol không thuộc hội thoại nguồn → 404, không lộ tin.
        var result = await messages.ForwardAsync(
            carol.Id,
            carolConversation,
            new ForwardMessageRequest(source.Id, Guid.CreateVersion7()));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Forward_RecalledMessage_IsAlreadyDeleted()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var source = await SendAsync(messages, alice.Id, conversationId, "sẽ thu hồi");
        await messages.RecallAsync(alice.Id, conversationId, source.Id);

        var result = await messages.ForwardAsync(
            bob.Id,
            conversationId,
            new ForwardMessageRequest(source.Id, Guid.CreateVersion7()));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageAlreadyDeleted, result.ErrorCode);
    }

    [Fact]
    public async Task Forward_IsIdempotentByClientMessageId()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var source = await SendAsync(messages, alice.Id, conversationId, "chuyển 2 lần");
        var clientMessageId = Guid.CreateVersion7();
        notifier.Messages.Clear();

        var first = await messages.ForwardAsync(
            bob.Id, conversationId, new ForwardMessageRequest(source.Id, clientMessageId));
        var second = await messages.ForwardAsync(
            bob.Id, conversationId, new ForwardMessageRequest(source.Id, clientMessageId));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.False(second.Value!.WasCreated);
        Assert.Equal(first.Value!.Message.Id, second.Value.Message.Id);
        // Chỉ một sự kiện tin mới cho lần chuyển tiếp thật.
        Assert.Single(notifier.Messages);
    }

    // --- Tìm kiếm ---

    [Fact]
    public async Task SearchInConversation_MatchesVietnameseWithoutAccents()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var match = await SendAsync(messages, alice.Id, conversationId, "Cuộc họp nhóm lúc 9 giờ");
        await SendAsync(messages, bob.Id, conversationId, "Tin không liên quan");

        var result = await messages.SearchAsync(alice.Id, conversationId, "HOP", null, 20);

        Assert.True(result.Succeeded);
        var hit = Assert.Single(result.Value!.Items);
        Assert.Equal(match.Id, hit.MessageId);
        Assert.Equal("Bob", hit.PeerDisplayName);
        Assert.Equal("Direct", hit.ConversationType);
        Assert.Contains("họp", hit.ContentPreview);
    }

    [Fact]
    public async Task Search_ExcludesRecalledMessages()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var match = await SendAsync(messages, alice.Id, conversationId, "Tin bí mật cần xoá");
        await messages.RecallAsync(alice.Id, conversationId, match.Id);

        var result = await messages.SearchAsync(alice.Id, conversationId, "bi mat", null, 20);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!.Items);
    }

    [Fact]
    public async Task GlobalSearch_OnlyReturnsConversationsIAmMemberOf()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var carol = NewUser("carol", "Carol");
        db.Users.Add(carol);
        await BefriendAsync(db, bob, carol);

        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversations = Conversations(db, notifier);
        var bobCarol = (await conversations.OpenDirectAsync(bob.Id, carol.Id)).Value!.Conversation.Id;

        await SendAsync(messages, alice.Id, conversationId, "cụm từ khoá chung");
        await SendAsync(messages, bob.Id, bobCarol, "cụm từ khoá chung");

        var aliceResults = await messages.SearchAsync(alice.Id, null, "tu khoa chung", null, 20);
        var carolResults = await messages.SearchAsync(carol.Id, null, "tu khoa chung", null, 20);

        Assert.True(aliceResults.Succeeded);
        Assert.Single(aliceResults.Value!.Items);
        Assert.Equal(conversationId, aliceResults.Value.Items[0].ConversationId);

        Assert.True(carolResults.Succeeded);
        Assert.Single(carolResults.Value!.Items);
        Assert.Equal(bobCarol, carolResults.Value.Items[0].ConversationId);
    }

    [Fact]
    public async Task Search_TooShortOrTooLongTerm_IsValidationFailed()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var messages = new MessageService(db, new NullChatNotifier());

        var tooShort = await messages.SearchAsync(alice.Id, conversationId, "a", null, 20);
        var tooLong = await messages.SearchAsync(alice.Id, conversationId, new string('a', 101), null, 20);

        Assert.Equal(ErrorCodes.ValidationFailed, tooShort.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationFailed, tooLong.ErrorCode);
    }

    [Fact]
    public async Task SearchInConversation_ByNonMember_IsNotFound()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var messages = new MessageService(db, new NullChatNotifier());
        var result = await messages.SearchAsync(outsider.Id, conversationId, "tin", null, 20);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConversationNotFound, result.ErrorCode);
    }

    // --- Ngữ cảnh quanh một tin ---

    [Fact]
    public async Task Context_ReturnsWindowAroundTargetAscending()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var messages = new MessageService(db, new NullChatNotifier());
        var ids = new List<Guid>();
        for (var index = 0; index < 10; index++)
        {
            ids.Add((await SendAsync(messages, alice.Id, conversationId, $"tin {index}")).Id);
        }

        var result = await messages.GetContextAsync(alice.Id, conversationId, ids[5], 5);

        Assert.True(result.Succeeded);
        var items = result.Value!.Items;
        Assert.Equal(5, items.Count);
        Assert.Equal(ids[3], items[0].Id);
        Assert.Equal(ids[4], items[1].Id);
        Assert.Equal(ids[5], items[2].Id);
        Assert.Equal(ids[6], items[3].Id);
        Assert.Equal(ids[7], items[4].Id);
        Assert.True(result.Value.HasMoreBefore);
        Assert.True(result.Value.HasMoreAfter);

        // Luôn sắp theo thời gian tăng dần.
        for (var index = 1; index < items.Count; index++)
        {
            Assert.True(items[index - 1].CreatedAt <= items[index].CreatedAt);
        }
    }

    [Fact]
    public async Task Context_MessageFromAnotherConversation_IsNotFound()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var carol = NewUser("carol", "Carol");
        db.Users.Add(carol);
        await BefriendAsync(db, bob, carol);
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier);
        var conversations = Conversations(db, notifier);
        var bobCarol = (await conversations.OpenDirectAsync(bob.Id, carol.Id)).Value!.Conversation.Id;
        var foreign = await SendAsync(messages, carol.Id, bobCarol, "tin hội thoại khác");

        var result = await messages.GetContextAsync(alice.Id, conversationId, foreign.Id, 5);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessageNotFound, result.ErrorCode);
    }

    // --- Tắt thông báo ---

    [Fact]
    public async Task Mute_SetsStateAndSuppressesPushOnly()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var notifications = new FakeNotificationService();
        var notifier = new RecordingChatNotifier();
        var messages = new MessageService(db, notifier, notifications);
        var conversations = Conversations(db, notifier);

        var muted = await conversations.MuteAsync(
            bob.Id, conversationId, new MuteConversationRequest("OneHour"));

        Assert.True(muted.Succeeded);
        Assert.True(muted.Value!.IsMuted);
        Assert.NotNull(muted.Value.MutedUntil);

        // Bob tắt thông báo → Alice gửi tin, Bob KHÔNG nhận push.
        await SendAsync(messages, alice.Id, conversationId, "tin khi đang tắt thông báo");
        Assert.Empty(notifications.Pushes);

        // Số chưa đọc vẫn tăng như bình thường.
        var summary = (await conversations.GetMyConversationsAsync(bob.Id, null, 20)).Value!
            .Items.Single(item => item.Id == conversationId);
        Assert.True(summary.IsMuted);
        Assert.Equal(1, summary.UnreadCount);

        // Bật lại → push quay lại cho Bob.
        var unmuted = await conversations.MuteAsync(
            bob.Id, conversationId, new MuteConversationRequest("Off"));
        Assert.True(unmuted.Succeeded);
        Assert.False(unmuted.Value!.IsMuted);
        Assert.Null(unmuted.Value.MutedUntil);

        await SendAsync(messages, alice.Id, conversationId, "tin sau khi bật lại");
        var push = Assert.Single(notifications.Pushes);
        Assert.Contains(bob.Id, push.UserIds);
        Assert.DoesNotContain(alice.Id, push.UserIds);
    }

    [Fact]
    public async Task Mute_ForeverAndInvalidDuration()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var conversations = Conversations(db, new RecordingChatNotifier());

        var forever = await conversations.MuteAsync(
            alice.Id, conversationId, new MuteConversationRequest("Forever"));
        Assert.True(forever.Succeeded);
        Assert.True(forever.Value!.IsMuted);
        Assert.Equal(9999, forever.Value.MutedUntil!.Value.Year);

        var invalid = await conversations.MuteAsync(
            alice.Id, conversationId, new MuteConversationRequest("TwoHours"));
        Assert.False(invalid.Succeeded);
        Assert.Equal(ErrorCodes.ValidationFailed, invalid.ErrorCode);
    }

    [Fact]
    public async Task Mute_ReflectedInConversationDetailAndRejectsNonMember()
    {
        var (db, alice, bob, conversationId) = await CreateConversationAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var conversations = Conversations(db, new RecordingChatNotifier());
        await conversations.MuteAsync(bob.Id, conversationId, new MuteConversationRequest("EightHours"));

        var detail = (await conversations.GetByIdAsync(bob.Id, conversationId)).Value!;
        Assert.True(detail.IsMuted);
        Assert.NotNull(detail.MutedUntil);

        var aliceDetail = (await conversations.GetByIdAsync(alice.Id, conversationId)).Value!;
        Assert.False(aliceDetail.IsMuted); // trạng thái của từng người

        var rejected = await conversations.MuteAsync(
            outsider.Id, conversationId, new MuteConversationRequest("OneHour"));
        Assert.False(rejected.Succeeded);
        Assert.Equal(ErrorCodes.ConversationNotFound, rejected.ErrorCode);
    }
}
