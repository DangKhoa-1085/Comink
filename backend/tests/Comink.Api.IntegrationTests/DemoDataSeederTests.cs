// Test seed dữ liệu demo (Mốc A): tạo đúng dữ liệu, idempotent, tôn trọng công tắc.
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class DemoDataSeederTests : IntegrationTestBase
{
    private const string DemoPassword = "MatKhau@123";

    public DemoDataSeederTests(PostgresFixture fixture) : base(fixture)
    {
    }

    /// <summary>Dựng DI Identity thật trên database riêng của test (giống IdentityServiceTests).</summary>
    private async Task<(ApplicationDbContext Db, UserManager<ApplicationUser> UserManager, ServiceProvider Provider)>
        CreateIdentityAsync()
    {
        var databaseName = $"comink_demo_seed_{Guid.NewGuid():N}";
        var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(Fixture.ConnectionString)
        {
            Database = databaseName
        }.ConnectionString;

        await using (var bootstrap = new Npgsql.NpgsqlConnection(Fixture.ConnectionString))
        {
            await bootstrap.OpenAsync();
            await using var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", bootstrap);
            await create.ExecuteNonQueryAsync();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-@";
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // Seed role giống ứng dụng thật (MigrationExtensions), vì seeder gán role "User".
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var roleName in new[] { "User", "Admin" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        return (db, userManager, provider);
    }

    [Fact]
    public async Task Seed_tao_du_lieu_mau_idempotent_va_ton_trong_cong_tac()
    {
        var (db, userManager, provider) = await CreateIdentityAsync();
        await using var _ = provider;
        var logger = NullLogger.Instance;

        // Tắt, hoặc bật nhưng thiếu mật khẩu → không tạo gì.
        Assert.False(await DemoDataSeeder.SeedAsync(db, userManager, enabled: false, password: DemoPassword, logger));
        Assert.False(await DemoDataSeeder.SeedAsync(db, userManager, enabled: true, password: null, logger));
        Assert.Equal(0, await db.Users.CountAsync());

        // Lần đầu: tạo đầy đủ dữ liệu mẫu.
        Assert.True(await DemoDataSeeder.SeedAsync(db, userManager, enabled: true, password: DemoPassword, logger));

        Assert.Equal(3, await db.Users.CountAsync(u => u.UserName!.EndsWith(".demo")));
        Assert.Equal(3, await db.Friendships.CountAsync());
        Assert.Equal(2, await db.Friendships.CountAsync(f => f.Status == FriendshipStatus.Accepted));
        Assert.Equal(3, await db.Follows.CountAsync());
        Assert.Equal(5, await db.Posts.CountAsync());
        Assert.Equal(3, await db.Comments.CountAsync());
        Assert.Equal(4, await db.PostLikes.CountAsync());
        Assert.Equal(3, await db.Conversations.CountAsync());
        Assert.Equal(8, await db.Messages.CountAsync());

        // Mọi hội thoại phải có tin cuối để danh sách chat hiển thị đúng.
        var conversations = await db.Conversations.AsNoTracking().ToListAsync();
        Assert.All(conversations, conversation =>
        {
            Assert.NotNull(conversation.LastMessageId);
            Assert.NotNull(conversation.LastMessageAt);
        });
        Assert.Single(conversations, c => c.Type == ConversationType.Group);

        // Cột đếm denormalized của bài viết.
        var anPost = await db.Posts.AsNoTracking().SingleAsync(p => p.Content!.StartsWith("Chào mừng"));
        Assert.Equal(2, anPost.LikeCount);
        Assert.Equal(2, anPost.CommentCount);
        Assert.Equal(1, anPost.ShareCount);

        // Cột đếm của hồ sơ.
        var an = await db.Users.AsNoTracking().SingleAsync(u => u.UserName == "an.demo");
        var anProfile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == an.Id);
        Assert.Equal(2, anProfile.FriendCount);
        Assert.Equal(2, anProfile.FollowerCount);
        Assert.Equal(1, anProfile.FollowingCount);

        // Lần hai: bỏ qua, không nhân đôi dữ liệu.
        Assert.False(await DemoDataSeeder.SeedAsync(db, userManager, enabled: true, password: DemoPassword, logger));
        Assert.Equal(3, await db.Users.CountAsync(u => u.UserName!.EndsWith(".demo")));
        Assert.Equal(5, await db.Posts.CountAsync());
        Assert.Equal(8, await db.Messages.CountAsync());
    }
}
