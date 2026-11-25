// Test tích hợp cho media: lưu cục bộ (dev), đăng ký tài sản và gắn ảnh vào bài viết.
using Comink.Application.Common;
using Comink.Application.Social.Dtos;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Media;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Social;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class MediaServiceTests : IntegrationTestBase
{
    public MediaServiceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    /// <summary>Môi trường giả để dựng LocalDiskMediaStorage mà không cần host thật.</summary>
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Comink.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static LocalDiskMediaStorage CreateLocalStorage(string rootPath) =>
        new(
            Options.Create(new MediaStorageOptions
            {
                LocalRootPath = rootPath,
                PublicBaseUrl = "/uploads"
            }),
            new TestHostEnvironment());

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

    private async Task<(ApplicationDbContext Db, ApplicationUser Owner)> CreateOwnerAsync()
    {
        var db = await CreateDbContextAsync();
        var owner = NewUser("owner.user", "Chủ Ảnh");
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        return (db, owner);
    }

    [Fact]
    public async Task SaveLocal_WritesFileAndReturnsPublicUrl()
    {
        var (db, owner) = await CreateOwnerAsync();
        var root = Path.Combine(Path.GetTempPath(), "comink-media-" + Guid.NewGuid().ToString("N"));
        var storage = CreateLocalStorage(root);
        var media = new MediaService(db, storage);
        var bytes = new byte[] { 1, 2, 3, 4 };

        await using var stream = new MemoryStream(bytes);
        var result = await media.SaveLocalAsync(
            owner.Id, "Post", "anh.png", "image/png", bytes.Length, stream);

        Assert.True(result.Succeeded);
        Assert.Equal("Local", result.Value!.Provider);
        Assert.StartsWith("/uploads/post/", result.Value.Url);

        var savedFile = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        Assert.Single(savedFile);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task SaveLocal_RejectsUnsupportedType()
    {
        var (db, owner) = await CreateOwnerAsync();
        var media = new MediaService(db, CreateLocalStorage(Path.GetTempPath()));

        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await media.SaveLocalAsync(owner.Id, "Post", "tailieu.pdf", "application/pdf", 3, stream);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MediaUnsupportedType, result.ErrorCode);
    }

    [Fact]
    public async Task SaveLocal_RejectsTooLarge()
    {
        var (db, owner) = await CreateOwnerAsync();
        var media = new MediaService(db, CreateLocalStorage(Path.GetTempPath()));

        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await media.SaveLocalAsync(
            owner.Id, "Post", "to.jpg", "image/jpeg", MediaLimits.MaxImageBytes + 1, stream);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.MediaTooLarge, result.ErrorCode);
    }

    [Fact]
    public async Task Register_CreatesAsset_AndIsIdempotent()
    {
        var (db, owner) = await CreateOwnerAsync();
        var media = new MediaService(db, CreateLocalStorage(Path.GetTempPath()));

        var request = new RegisterMediaRequest(
            ProviderPublicId: "abc123",
            Url: "/uploads/post/abc123.png",
            ThumbnailUrl: "/uploads/post/abc123.png",
            MimeType: "image/png",
            SizeBytes: 1024,
            Width: 800,
            Height: 600,
            Context: "Post");

        var first = await media.RegisterAsync(owner.Id, request);
        Assert.True(first.Succeeded);

        var second = await media.RegisterAsync(owner.Id, request);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
    }

    [Fact]
    public async Task CreatePost_WithOwnedMedia_AttachesImage()
    {
        var (db, owner) = await CreateOwnerAsync();
        var media = new MediaService(db, CreateLocalStorage(Path.GetTempPath()));
        var posts = new PostService(db);

        var asset = await media.RegisterAsync(owner.Id, new RegisterMediaRequest(
            "img1", "/uploads/post/img1.png", null, "image/png", 2048, 640, 480, "Post"));

        var post = await posts.CreateAsync(
            owner.Id,
            new CreatePostRequest("Bài có ảnh", "Public", [asset.Value!.Id], null));

        Assert.True(post.Succeeded);
        var attached = Assert.Single(post.Value!.Media);
        Assert.Equal("/uploads/post/img1.png", attached.Url);
    }

    [Fact]
    public async Task CreatePost_WithMediaOfAnotherUser_IsRejected()
    {
        var (db, owner) = await CreateOwnerAsync();
        var other = NewUser("other.user", "Người Khác");
        db.Users.Add(other);
        await db.SaveChangesAsync();

        var media = new MediaService(db, CreateLocalStorage(Path.GetTempPath()));
        var posts = new PostService(db);
        var othersAsset = await media.RegisterAsync(other.Id, new RegisterMediaRequest(
            "img2", "/uploads/post/img2.png", null, "image/png", 1024, null, null, "Post"));

        var result = await posts.CreateAsync(
            owner.Id,
            new CreatePostRequest("Dùng ảnh người khác", "Public", [othersAsset.Value!.Id], null));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.PostMediaNotOwned, result.ErrorCode);
    }
}
