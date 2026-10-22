// Test tích hợp cho chat nhóm (NC Giai đoạn 3 — docs/tasks/phase3_messaging.md mục 7):
// tạo nhóm từ bạn bè, giới hạn thành viên, danh sách hội thoại và quyền truy cập.
using Comink.Application.Common;
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class GroupConversationTests : IntegrationTestBase
{
    public GroupConversationTests(PostgresFixture fixture) : base(fixture)
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

    private static void MakeFriends(ApplicationDbContext db, ApplicationUser first, ApplicationUser second) =>
        db.Friendships.Add(new Friendship
        {
            RequesterId = first.Id,
            AddresseeId = second.Id,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow
        });

    /** Ba người, alice là bạn của bob và carol. */
    private async Task<(ApplicationDbContext Db, ApplicationUser Alice, ApplicationUser Bob, ApplicationUser Carol)>
        CreateTriangleAsync()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice", "Alice");
        var bob = NewUser("bob", "Bob");
        var carol = NewUser("carol", "Carol");
        db.Users.AddRange(alice, bob, carol);
        MakeFriends(db, alice, bob);
        MakeFriends(db, alice, carol);
        await db.SaveChangesAsync();

        return (db, alice, bob, carol);
    }

    [Fact]
    public async Task CreateGroup_CreatesGroupWithOwnerAndMembers()
    {
        var (db, alice, bob, carol) = await CreateTriangleAsync();

        var result = await new ConversationService(db)
            .CreateGroupAsync(alice.Id, new CreateGroupConversationRequest("Nhom hoc tap", [bob.Id, carol.Id]));

        Assert.True(result.Succeeded);
        Assert.Equal("Group", result.Value!.Type);
        Assert.Equal("Nhom hoc tap", result.Value.Title);
        Assert.Equal(3, result.Value.Members.Count);

        var stored = await db.Conversations.FindAsync(result.Value.Id);
        Assert.Equal(ConversationType.Group, stored!.Type);

        var ownerMember = db.ConversationMembers.Single(m => m.ConversationId == stored.Id && m.UserId == alice.Id);
        Assert.Equal(MemberRole.Owner, ownerMember.Role);
    }

    [Fact]
    public async Task CreateGroup_TooFewMembers_IsRejected()
    {
        var (db, alice, bob, _) = await CreateTriangleAsync();

        var result = await new ConversationService(db)
            .CreateGroupAsync(alice.Id, new CreateGroupConversationRequest("Chi hai nguoi", [bob.Id]));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessagingGroupTooFew, result.ErrorCode);
    }

    [Fact]
    public async Task CreateGroup_TooManyMembers_IsRejected()
    {
        var db = await CreateDbContextAsync();
        var alice = NewUser("alice", "Alice");
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        // 10 người được mời + người tạo = 11 > 10 → bị chặn trước khi kiểm tra bạn bè.
        var invitees = Enumerable.Range(0, 10).Select(index => Guid.CreateVersion7()).ToList();

        var result = await new ConversationService(db)
            .CreateGroupAsync(alice.Id, new CreateGroupConversationRequest("Nhom dong", invitees));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessagingGroupTooMany, result.ErrorCode);
    }

    [Fact]
    public async Task CreateGroup_WithNonFriend_IsForbidden()
    {
        var (db, alice, bob, _) = await CreateTriangleAsync();
        var stranger = NewUser("stranger", "Stranger");
        db.Users.Add(stranger);
        await db.SaveChangesAsync();

        var result = await new ConversationService(db)
            .CreateGroupAsync(alice.Id, new CreateGroupConversationRequest("Nhom la", [bob.Id, stranger.Id]));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MessagingNotFriends, result.ErrorCode);
    }

    [Fact]
    public async Task GetMyConversations_IncludesGroupWithMemberCountAndNoPeer()
    {
        var (db, alice, bob, carol) = await CreateTriangleAsync();
        var service = new ConversationService(db);
        var created = await service.CreateGroupAsync(
            alice.Id,
            new CreateGroupConversationRequest("Nhom ba nguoi", [bob.Id, carol.Id]));

        var list = await service.GetMyConversationsAsync(alice.Id, null, 20);

        Assert.True(list.Succeeded);
        var group = Assert.Single(list.Value!.Items, item => item.Id == created.Value!.Id);
        Assert.Equal("Group", group.Type);
        Assert.Equal("Nhom ba nguoi", group.Title);
        Assert.Null(group.Peer);
        Assert.Equal(3, group.MemberCount);
    }

    [Fact]
    public async Task GetById_ByOutsider_IsNotFound()
    {
        var (db, alice, bob, carol) = await CreateTriangleAsync();
        var outsider = NewUser("mallory", "Mallory");
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var service = new ConversationService(db);
        var created = await service.CreateGroupAsync(
            alice.Id,
            new CreateGroupConversationRequest("Nhom kin", [bob.Id, carol.Id]));

        var byOutsider = await service.GetByIdAsync(outsider.Id, created.Value!.Id);

        Assert.False(byOutsider.Succeeded);
        Assert.Equal(ErrorCodes.ConversationNotFound, byOutsider.ErrorCode);
    }
}
