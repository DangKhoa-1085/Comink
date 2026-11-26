// Test tích hợp cho module Thông báo: thiết bị, hộp thư và push (docs/05 mục 10).
using Comink.Application.Common.Interfaces;
using Comink.Application.Notifications.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Notifications;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
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

    private static NotificationService NewService(
        ApplicationDbContext db,
        FakePushSender push) =>
        new(db, push, NullLogger<NotificationService>.Instance);

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
}
