// Test tích hợp cho module Thông báo: thiết bị, hộp thư, push và sự kiện SignalR (docs/05 mục 10).
using Comink.Application.Common.Interfaces;
using Comink.Application.Notifications.Abstractions;
using Comink.Application.Notifications.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Notifications;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Comink.Infrastructure.Social;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class NotificationServiceTests : IntegrationTestBase
{
    public NotificationServiceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private sealed class FakePushSender : IPushSender
    {
        public List<(IReadOnlyList<string> Tokens, PushNotification Notification)> Batches { get; } = [];

        public Task SendToDeviceAsync(
            string deviceToken,
            PushNotification notification,
            CancellationToken cancellationToken = default)
        {
            Batches.Add(([deviceToken], notification));
            return Task.CompletedTask;
        }

        public Task SendToDevicesAsync(
            IEnumerable<string> deviceTokens,
            PushNotification notification,
            CancellationToken cancellationToken = default)
        {
            Batches.Add((deviceTokens.ToList(), notification));
            return Task.CompletedTask;
        }
    }

    private static ApplicationUser NewUser(string userName)
    {
        var id = Guid.CreateVersion7();
        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@example.test",
            NormalizedEmail = $"{userName.ToUpperInvariant()}@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString(),
            Profile = new UserProfile
            {
                UserId = id,
                DisplayName = userName,
            },
        };
    }

    private sealed class FakeNotificationNotifier : INotificationNotifier
    {
        public bool ShouldThrow { get; set; }
        public List<(Guid RecipientId, NotificationDto Notification)> Created { get; } = [];
        public List<(IReadOnlyCollection<Guid> UserIds, Guid? ActorId, string Action)> FriendshipChanges { get; } = [];

        public Task NotificationCreatedAsync(
            Guid recipientId,
            NotificationDto notification,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("SignalR unavailable");
            }
            Created.Add((recipientId, notification));
            return Task.CompletedTask;
        }

        public Task FriendshipChangedAsync(
            IReadOnlyCollection<Guid> userIds,
            Guid? actorId,
            string action,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("SignalR unavailable");
            }
            FriendshipChanges.Add((userIds, actorId, action));
            return Task.CompletedTask;
        }
    }

    private static NotificationService NewService(
        ApplicationDbContext db,
        FakePushSender push,
        INotificationNotifier? notifier = null) =>
        new(db, push, NullLogger<NotificationService>.Instance, notifier);

    [Fact]
    public async Task RegisterDevice_UpsertsTokenAndMovesToCurrentUser()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice");
        var bob = NewUser("bob");
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var push = new FakePushSender();
        var service = NewService(db, push);

        var first = await service.RegisterDeviceAsync(
            alice.Id,
            new RegisterDeviceRequest("token-1234567890", "Android", "Pixel"));
        var second = await service.RegisterDeviceAsync(
            bob.Id,
            new RegisterDeviceRequest("token-1234567890", "Android", "Pixel"));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        var device = db.UserDevices.Single();
        Assert.Equal(bob.Id, device.UserId);
        Assert.True(device.IsActive);
    }

    [Fact]
    public async Task CreateAndPush_WritesInboxAndSendsToActiveDevices()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice");
        var bob = NewUser("bob");
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var push = new FakePushSender();
        var service = NewService(db, push);
        await service.RegisterDeviceAsync(
            bob.Id,
            new RegisterDeviceRequest("token-bob-123456", "Android", "Pixel"));

        await service.CreateAndPushAsync(
            bob.Id,
            alice.Id,
            NotificationType.FriendRequestReceived,
            "Friendship",
            Guid.CreateVersion7(),
            "Lời mời kết bạn",
            "alice đã gửi lời mời kết bạn",
            new Dictionary<string, string> { ["type"] = "FriendRequest" });

        var notification = db.Notifications.Single();
        Assert.Equal(bob.Id, notification.RecipientId);
        Assert.Equal(NotificationType.FriendRequestReceived, notification.Type);
        Assert.False(notification.IsRead);

        var batch = Assert.Single(push.Batches);
        Assert.Contains("token-bob-123456", batch.Tokens);
        Assert.Equal("Lời mời kết bạn", batch.Notification.Title);
    }

    [Fact]
    public async Task ListAndMarkRead_OnlyAffectOwnNotifications()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice");
        var bob = NewUser("bob");
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var service = NewService(db, new FakePushSender());
        var notification = new Notification
        {
            RecipientId = bob.Id,
            ActorId = alice.Id,
            Type = NotificationType.FriendRequestReceived,
            EntityType = "Friendship",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var aliceList = await service.GetListAsync(alice.Id, null, 20);
        var bobList = await service.GetListAsync(bob.Id, null, 20);

        Assert.Empty(aliceList.Value!.Items);
        Assert.Single(bobList.Value!.Items);
        Assert.Equal("alice", bobList.Value!.Items[0].ActorDisplayName);

        var read = await service.MarkReadAsync(bob.Id, notification.Id);
        var outsiderRead = await service.MarkReadAsync(alice.Id, notification.Id);

        Assert.True(read.Succeeded);
        Assert.False(outsiderRead.Succeeded);
        Assert.True(db.Notifications.Single().IsRead);
        Assert.NotNull(db.Notifications.Single().ReadAt);
    }

    [Fact]
    public async Task UnregisterDevice_MakesDeviceInactive()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice");
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        var service = NewService(db, new FakePushSender());
        await service.RegisterDeviceAsync(
            alice.Id,
            new RegisterDeviceRequest("token-alice-123456", "iOS", "iPhone"));

        var result = await service.UnregisterDeviceAsync(alice.Id, "token-alice-123456");

        Assert.True(result.Succeeded);
        Assert.False(db.UserDevices.Single().IsActive);
    }

    [Fact]
    public async Task CreateAndPush_EmitsRealtimeNotificationWithActorInfo_AndIgnoresNotifierFailure()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice");
        alice.Profile!.DisplayName = "Alice Nguyen";
        alice.Profile.AvatarUrl = "https://cdn.example.test/alice.jpg";
        var bob = NewUser("bob");
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var notifier = new FakeNotificationNotifier();
        var service = NewService(db, new FakePushSender(), notifier);

        await service.CreateAndPushAsync(
            bob.Id,
            alice.Id,
            NotificationType.FriendRequestReceived,
            "Friendship",
            Guid.CreateVersion7(),
            "Lời mời kết bạn",
            "Alice Nguyen đã gửi lời mời kết bạn",
            new Dictionary<string, string> { ["type"] = "FriendRequest" });

        var emitted = Assert.Single(notifier.Created);
        Assert.Equal(bob.Id, emitted.RecipientId);
        Assert.Equal("FriendRequestReceived", emitted.Notification.Type);
        Assert.Equal("Alice Nguyen", emitted.Notification.ActorDisplayName);
        Assert.Equal("https://cdn.example.test/alice.jpg", emitted.Notification.ActorAvatarUrl);

        notifier.ShouldThrow = true;
        await service.CreateAndPushAsync(
            bob.Id,
            alice.Id,
            NotificationType.FriendRequestAccepted,
            "Friendship",
            Guid.CreateVersion7(),
            "Đã chấp nhận",
            "Alice Nguyen đã chấp nhận",
            null);
        Assert.Equal(2, db.Notifications.Count());
    }

    [Fact]
    public async Task FriendshipService_EmitsNotificationAndFriendshipChangedEvents()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice");
        var bob = NewUser("bob");
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var notifier = new FakeNotificationNotifier();
        var notificationService = NewService(db, new FakePushSender(), notifier);
        var friendships = new FriendshipService(db, notificationService, notifier);

        var sendResult = await friendships.SendRequestAsync(alice.Id, bob.Id);
        Assert.True(sendResult.Succeeded);
        Assert.Contains(notifier.Created, c => c.RecipientId == bob.Id && c.Notification.Type == "FriendRequestReceived");
        Assert.Contains(notifier.FriendshipChanges, c => c.Action == "RequestSent" && c.UserIds.Contains(alice.Id) && c.UserIds.Contains(bob.Id));

        var acceptResult = await friendships.AcceptAsync(bob.Id, sendResult.Value!.Id);
        Assert.True(acceptResult.Succeeded);
        Assert.Contains(notifier.Created, c => c.RecipientId == alice.Id && c.Notification.Type == "FriendRequestAccepted");
        Assert.Contains(notifier.FriendshipChanges, c => c.Action == "RequestAccepted");

        var removeResult = await friendships.RemoveFriendAsync(alice.Id, bob.Id);
        Assert.True(removeResult.Succeeded);
        Assert.Contains(notifier.FriendshipChanges, c => c.Action == "FriendRemoved");
    }
}
