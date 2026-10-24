// Test tích hợp cho quy tắc "tài khoản Admin ẩn" (bổ sung Giai đoạn 5, chốt 2026-10-24):
// - không xuất hiện trong danh bạ/tìm kiếm với bất kỳ ai (kể cả admin khác);
// - hồ sơ/bài viết trên trang cá nhân chỉ hiện với chính họ hoặc Admin khác;
// - người dùng thường không kết bạn/theo dõi/nhắn tin/mời vào nhóm được tài khoản Admin.
using Comink.Application.Common;
using Comink.Application.Messaging.Dtos;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Messaging;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Social;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class AdminVisibilityTests : IntegrationTestBase
{
    public AdminVisibilityTests(PostgresFixture fixture) : base(fixture)
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

    /// <summary>Một người dùng thường + hai tài khoản Admin (một để xem, một để làm người xem).</summary>
    private async Task<(ApplicationDbContext Db, ApplicationUser Normal, ApplicationUser Admin, ApplicationUser OtherAdmin)>
        CreateUsersAsync()
    {
        var db = await CreateDbContextAsync();
        var normal = NewUser("normal.user", "Người Thường");
        var admin = NewUser("secret.admin", "Quản Trị Ẩn");
        var otherAdmin = NewUser("other.admin", "Quản Trị Khác");
        db.Users.AddRange(normal, admin, otherAdmin);

        var adminRole = new IdentityRole<Guid>
        {
            Id = Guid.CreateVersion7(),
            Name = "Admin",
            NormalizedName = "ADMIN"
        };
        db.Roles.Add(adminRole);
        await db.SaveChangesAsync();

        db.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = adminRole.Id },
            new IdentityUserRole<Guid> { UserId = otherAdmin.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync();

        return (db, normal, admin, otherAdmin);
    }

    [Fact]
    public async Task Search_HidesAdminAccounts_ForEveryone_ButStillFindsNormalUsers()
    {
        var (db, normal, admin, otherAdmin) = await CreateUsersAsync();
        var service = new UserDirectoryService(db);

        // Người dùng thường tìm theo username của admin → không thấy.
        var asNormal = await service.SearchAsync(normal.Id, "secret", null, 20);
        Assert.True(asNormal.Succeeded);
        Assert.DoesNotContain(asNormal.Value!.Items, item => item.Id == admin.Id);

        // Admin khác cũng không thấy admin trong danh bạ (chốt 2026-10-24).
        var asAdmin = await service.SearchAsync(otherAdmin.Id, "secret", null, 20);
        Assert.True(asAdmin.Succeeded);
        Assert.DoesNotContain(asAdmin.Value!.Items, item => item.Id == admin.Id);

        // Bộ lọc không được ẩn nhầm tài khoản thường.
        var normalAsAdmin = await service.SearchAsync(admin.Id, "normal", null, 20);
        Assert.True(normalAsAdmin.Succeeded);
        Assert.Contains(normalAsAdmin.Value!.Items, item => item.Id == normal.Id);
    }

    [Fact]
    public async Task PublicProfile_OfAdmin_IsHiddenFromNormalUser_ButVisibleToAdminAndSelf()
    {
        var (db, normal, admin, otherAdmin) = await CreateUsersAsync();
        var service = new UserDirectoryService(db);

        // Người thường mở trực tiếp hồ sơ admin → 404 (không dùng 403 để không lộ tồn tại).
        var hidden = await service.GetPublicProfileAsync(normal.Id, admin.Id);
        Assert.False(hidden.Succeeded);
        Assert.Equal(ErrorCodes.NotFound, hidden.ErrorCode);

        // Admin khác xem được (phục vụ quản trị).
        var byAdmin = await service.GetPublicProfileAsync(otherAdmin.Id, admin.Id);
        Assert.True(byAdmin.Succeeded);
        Assert.Equal(admin.Id, byAdmin.Value!.Id);

        // Chính admin xem hồ sơ của mình vẫn được.
        var bySelf = await service.GetPublicProfileAsync(admin.Id, admin.Id);
        Assert.True(bySelf.Succeeded);
    }

    [Fact]
    public async Task NormalUser_CannotFriendFollowOrMessageAdmin()
    {
        var (db, normal, admin, _) = await CreateUsersAsync();

        var friendRequest = await new FriendshipService(db).SendRequestAsync(normal.Id, admin.Id);
        Assert.False(friendRequest.Succeeded);
        Assert.Equal(ErrorCodes.NotFound, friendRequest.ErrorCode);

        var follow = await new FollowService(db).FollowAsync(normal.Id, admin.Id);
        Assert.False(follow.Succeeded);
        Assert.Equal(ErrorCodes.NotFound, follow.ErrorCode);

        var direct = await new ConversationService(db).OpenDirectAsync(normal.Id, admin.Id);
        Assert.False(direct.Succeeded);
        Assert.Equal(ErrorCodes.NotFound, direct.ErrorCode);
    }

    [Fact]
    public async Task GroupInvite_WithAdmin_FromNormalUser_IsRejected()
    {
        // Hai người được mời (admin + admin khác) + người tạo = 3 thành viên ⇒ qua kiểm tra số lượng,
        // rồi bị chặn ở bước kiểm tra tài khoản ẩn.
        var (db, normal, admin, otherAdmin) = await CreateUsersAsync();

        var group = await new ConversationService(db).CreateGroupAsync(
            normal.Id,
            new CreateGroupConversationRequest("Nhóm thử", [admin.Id, otherAdmin.Id]));

        Assert.False(group.Succeeded);
        Assert.Equal(ErrorCodes.NotFound, group.ErrorCode);
    }

    [Fact]
    public async Task UserPosts_OfAdmin_AreHiddenFromNormalUser()
    {
        var (db, normal, admin, _) = await CreateUsersAsync();

        var result = await new PostService(db).GetUserPostsAsync(normal.Id, admin.Id, null, 20);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Admin_CanViewAndInteractWithAnotherAdmin()
    {
        var (db, _, admin, otherAdmin) = await CreateUsersAsync();

        // Admin xem hồ sơ admin khác được (chỉ danh bạ là ẩn).
        var profile = await new UserDirectoryService(db).GetPublicProfileAsync(otherAdmin.Id, admin.Id);
        Assert.True(profile.Succeeded);

        // Không chặn nhầm công cụ quản trị: admin gửi lời mời cho admin khác vẫn hợp lệ.
        var friendRequest = await new FriendshipService(db).SendRequestAsync(otherAdmin.Id, admin.Id);
        Assert.True(friendRequest.Succeeded);
    }
}
