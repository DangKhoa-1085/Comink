// Test tích hợp cho nghiệp vụ mạng xã hội Giai đoạn 2 trên PostgreSQL thật
// (migration M1 đã áp nên có đủ bảng, chỉ mục và hàm unaccent).
using Comink.Application.Common;
using Comink.Application.Social.Dtos;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Social;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class SocialServiceTests : IntegrationTestBase
{
    public SocialServiceTests(PostgresFixture fixture) : base(fixture)
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

    private async Task<(ApplicationDbContext Db, ApplicationUser First, ApplicationUser Second)>
        CreateTwoUsersAsync()
    {
        var db = await CreateDbContextAsync();
        var first = NewUser("first.user", "Khoa Nguyễn");
        var second = NewUser("second.user", "Minh Trần");
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();
        return (db, first, second);
    }

    [Fact]
    public async Task Search_FindsUser_WhenKeywordHasNoAccents()
    {
        var (db, first, second) = await CreateTwoUsersAsync();
        var service = new UserDirectoryService(db);

        var result = await service.SearchAsync(second.Id, "khoa nguyen", null, 20);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Value!.Items, item => item.Id == first.Id);
    }

    [Fact]
    public async Task Search_FindsUser_ByUserName_CaseInsensitive()
    {
        var (db, first, _) = await CreateTwoUsersAsync();
        var service = new UserDirectoryService(db);

        var result = await service.SearchAsync(Guid.CreateVersion7(), "FIRST", null, 20);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Value!.Items, item => item.Id == first.Id);
    }

    [Fact]
    public async Task SendRequest_ToSelf_IsRejected()
    {
        var (db, first, _) = await CreateTwoUsersAsync();
        var service = new FriendshipService(db);

        var result = await service.SendRequestAsync(first.Id, first.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.SocialCannotFriendSelf, result.ErrorCode);
    }

    [Fact]
    public async Task SendRequest_ThenAccept_MakesFriendsAndUpdatesCounts()
    {
        var (db, first, second) = await CreateTwoUsersAsync();
        var service = new FriendshipService(db);

        var sent = await service.SendRequestAsync(first.Id, second.Id);
        Assert.True(sent.Succeeded);
        Assert.Equal("Pending", sent.Value!.Status);

        // Gửi lại: idempotent, không tạo dòng thứ hai
        var resent = await service.SendRequestAsync(first.Id, second.Id);
        Assert.True(resent.Succeeded);
        Assert.Equal(sent.Value.Id, resent.Value!.Id);
        Assert.Equal(1, await db.Friendships.AsNoTracking().CountAsync());

        var accepted = await service.AcceptAsync(second.Id, sent.Value.Id);
        Assert.True(accepted.Succeeded);
        Assert.Equal("Accepted", accepted.Value!.Status);

        var counts = await db.UserProfiles.AsNoTracking().ToDictionaryAsync(p => p.UserId, p => p.FriendCount);
        Assert.Equal(1, counts[first.Id]);
        Assert.Equal(1, counts[second.Id]);

        // Đã là bạn thì không gửi lời mời mới được
        var again = await service.SendRequestAsync(first.Id, second.Id);
        Assert.False(again.Succeeded);
        Assert.Equal(ErrorCodes.SocialAlreadyFriends, again.ErrorCode);
    }

    [Fact]
    public async Task SendRequest_WhenOpponentAlreadyInvited_AutoAccepts()
    {
        var (db, first, second) = await CreateTwoUsersAsync();
        var service = new FriendshipService(db);

        await service.SendRequestAsync(first.Id, second.Id);
        var result = await service.SendRequestAsync(second.Id, first.Id);

        Assert.True(result.Succeeded);
        Assert.Equal("Accepted", result.Value!.Status);
        Assert.Equal(1, await db.Friendships.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Accept_ByRequester_IsForbidden()
    {
        var (db, first, second) = await CreateTwoUsersAsync();
        var service = new FriendshipService(db);
        var sent = await service.SendRequestAsync(first.Id, second.Id);

        var result = await service.AcceptAsync(first.Id, sent.Value!.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.SocialNotRequestAddressee, result.ErrorCode);
    }

    [Fact]
    public async Task RemoveFriend_DecrementsCounts()
    {
        var (db, first, second) = await CreateTwoUsersAsync();
        var service = new FriendshipService(db);
        var sent = await service.SendRequestAsync(first.Id, second.Id);
        await service.AcceptAsync(second.Id, sent.Value!.Id);

        var removed = await service.RemoveFriendAsync(first.Id, second.Id);

        Assert.True(removed.Succeeded);
        Assert.Equal(0, await db.Friendships.AsNoTracking().CountAsync());
        var counts = await db.UserProfiles.AsNoTracking().ToDictionaryAsync(p => p.UserId, p => p.FriendCount);
        Assert.Equal(0, counts[first.Id]);
        Assert.Equal(0, counts[second.Id]);
    }

    [Fact]
    public async Task IncomingRequests_ListsPendingInvitation()
    {
        var (db, first, second) = await CreateTwoUsersAsync();
        var service = new FriendshipService(db);
        await service.SendRequestAsync(first.Id, second.Id);

        var page = await service.GetIncomingRequestsAsync(second.Id, null, 20);

        Assert.True(page.Succeeded);
        var item = Assert.Single(page.Value!.Items);
        Assert.Equal(first.Id, item.User.Id);
        Assert.Equal("Pending", item.Status);
    }

    [Fact]
    public async Task Follow_And_Unfollow_UpdateCounters()
    {
        var (db, first, second) = await CreateTwoUsersAsync();
        var service = new FollowService(db);

        var followed = await service.FollowAsync(first.Id, second.Id);
        Assert.True(followed.Succeeded);

        var profiles = await db.UserProfiles.AsNoTracking().ToListAsync();
        Assert.Equal(1, profiles.Single(p => p.UserId == second.Id).FollowerCount);
        Assert.Equal(1, profiles.Single(p => p.UserId == first.Id).FollowingCount);

        // Gọi lại không được tăng đúp
        await service.FollowAsync(first.Id, second.Id);
        var afterRepeat = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == second.Id);
        Assert.Equal(1, afterRepeat.FollowerCount);

        await service.UnfollowAsync(first.Id, second.Id);
        var afterUnfollow = await db.UserProfiles.AsNoTracking().ToListAsync();
        Assert.Equal(0, afterUnfollow.Single(p => p.UserId == second.Id).FollowerCount);
        Assert.Equal(0, afterUnfollow.Single(p => p.UserId == first.Id).FollowingCount);
    }

    [Fact]
    public async Task Follow_Self_IsRejected()
    {
        var (db, first, _) = await CreateTwoUsersAsync();
        var service = new FollowService(db);

        var result = await service.FollowAsync(first.Id, first.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.SocialCannotFollowSelf, result.ErrorCode);
    }

    [Fact]
    public async Task PublicProfile_ReportsFriendshipState()
    {
        var (db, first, second) = await CreateTwoUsersAsync();
        var friendships = new FriendshipService(db);
        var directory = new UserDirectoryService(db);
        var sent = await friendships.SendRequestAsync(first.Id, second.Id);
        await friendships.AcceptAsync(second.Id, sent.Value!.Id);

        var profile = await directory.GetPublicProfileAsync(second.Id, first.Id);

        Assert.True(profile.Succeeded);
        Assert.Equal(FriendshipRelation.Friends, profile.Value!.Relationship.Friendship);
        Assert.Equal(1, profile.Value.FriendCount);
    }

    [Fact]
    public async Task PublicProfile_WhenUserMissing_ReturnsNotFound()
    {
        var (db, first, _) = await CreateTwoUsersAsync();
        var directory = new UserDirectoryService(db);

        var profile = await directory.GetPublicProfileAsync(first.Id, Guid.CreateVersion7());

        Assert.False(profile.Succeeded);
        Assert.Equal(ErrorCodes.NotFound, profile.ErrorCode);
    }
}
