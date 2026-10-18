// Test tích hợp cho bài viết, bảng tin, thích và bình luận (Giai đoạn 2).
using Comink.Application.Common;
using Comink.Application.Social.Dtos;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Social;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class PostServiceTests : IntegrationTestBase
{
    public PostServiceTests(PostgresFixture fixture) : base(fixture)
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

    /// <summary>Tạo hai người dùng đã là bạn bè (A và B).</summary>
    private async Task<(ApplicationDbContext Db, ApplicationUser A, ApplicationUser B)> CreateFriendPairAsync()
    {
        var db = await CreateDbContextAsync();
        var a = NewUser("a.user", "An Nguyễn");
        var b = NewUser("b.user", "Bình Trần");
        db.Users.AddRange(a, b);
        await db.SaveChangesAsync();

        var friendships = new FriendshipService(db);
        var sent = await friendships.SendRequestAsync(a.Id, b.Id);
        await friendships.AcceptAsync(b.Id, sent.Value!.Id);

        return (db, a, b);
    }

    private async Task<ApplicationUser> AddStrangerAsync(ApplicationDbContext db)
    {
        var stranger = NewUser("c.user", "Chi Lê");
        db.Users.Add(stranger);
        await db.SaveChangesAsync();
        return stranger;
    }

    [Fact]
    public async Task CreatePost_FriendsVisibility_VisibleToFriend_ButNotStranger()
    {
        var (db, a, b) = await CreateFriendPairAsync();
        var stranger = await AddStrangerAsync(db);
        var posts = new PostService(db);

        var created = await posts.CreateAsync(a.Id, new CreatePostRequest("Xin chào", "Friends", null, null));
        Assert.True(created.Succeeded);
        Assert.Equal("Friends", created.Value!.Visibility);

        var byFriend = await posts.GetByIdAsync(b.Id, created.Value.Id);
        Assert.True(byFriend.Succeeded);

        // Người lạ không xem được bài Friends — trả POST_NOT_FOUND (không tiết lộ sự tồn tại).
        var byStranger = await posts.GetByIdAsync(stranger.Id, created.Value.Id);
        Assert.False(byStranger.Succeeded);
        Assert.Equal(ErrorCodes.PostNotFound, byStranger.ErrorCode);
    }

    [Fact]
    public async Task CreatePost_EmptyContentWithoutMedia_IsRejected()
    {
        var (db, a, _) = await CreateFriendPairAsync();
        var posts = new PostService(db);

        var result = await posts.CreateAsync(a.Id, new CreatePostRequest("   ", null, null, null));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.PostContentRequired, result.ErrorCode);
    }

    [Fact]
    public async Task Feed_ContainsFriendsPost_ButNotOnlyMe_OrStrangerPost()
    {
        var (db, a, b) = await CreateFriendPairAsync();
        var stranger = await AddStrangerAsync(db);
        var posts = new PostService(db);

        await posts.CreateAsync(a.Id, new CreatePostRequest("Bạn bè thấy", "Friends", null, null));
        await posts.CreateAsync(a.Id, new CreatePostRequest("Chỉ mình tôi", "OnlyMe", null, null));
        await posts.CreateAsync(stranger.Id, new CreatePostRequest("Người lạ công khai", "Public", null, null));

        var feed = await posts.GetFeedAsync(b.Id, null, 20);

        Assert.True(feed.Succeeded);
        var contents = feed.Value!.Items.Select(item => item.Content).ToList();
        Assert.Contains("Bạn bè thấy", contents);
        Assert.DoesNotContain("Chỉ mình tôi", contents);
        Assert.DoesNotContain("Người lạ công khai", contents);
    }

    [Fact]
    public async Task Feed_UsesCursorPagination()
    {
        var (db, a, _) = await CreateFriendPairAsync();
        var posts = new PostService(db);
        await posts.CreateAsync(a.Id, new CreatePostRequest("Bài 1", "Public", null, null));
        await posts.CreateAsync(a.Id, new CreatePostRequest("Bài 2", "Public", null, null));

        var first = await posts.GetFeedAsync(a.Id, null, 1);
        Assert.True(first.Value!.HasMore);
        Assert.NotNull(first.Value.NextCursor);

        var second = await posts.GetFeedAsync(a.Id, first.Value.NextCursor, 1);
        Assert.Single(second.Value!.Items);
        Assert.NotEqual(first.Value.Items[0].Id, second.Value.Items[0].Id);
    }

    [Fact]
    public async Task Like_IsIdempotent_AndUpdatesCounter()
    {
        var (db, a, b) = await CreateFriendPairAsync();
        var posts = new PostService(db);
        var likes = new LikeService(db);
        var post = await posts.CreateAsync(a.Id, new CreatePostRequest("Bài", "Public", null, null));

        await likes.LikeAsync(b.Id, post.Value!.Id);
        await likes.LikeAsync(b.Id, post.Value.Id);

        var byAuthor = await posts.GetByIdAsync(a.Id, post.Value.Id);
        Assert.Equal(1, byAuthor.Value!.LikeCount);
        Assert.False(byAuthor.Value.LikedByMe);

        var byLiker = await posts.GetByIdAsync(b.Id, post.Value.Id);
        Assert.True(byLiker.Value!.LikedByMe);

        await likes.UnlikeAsync(b.Id, post.Value.Id);
        var afterUnlike = await posts.GetByIdAsync(a.Id, post.Value.Id);
        Assert.Equal(0, afterUnlike.Value!.LikeCount);
    }

    [Fact]
    public async Task Like_PostNotVisible_ReturnsNotFound()
    {
        var (db, a, _) = await CreateFriendPairAsync();
        var stranger = await AddStrangerAsync(db);
        var posts = new PostService(db);
        var likes = new LikeService(db);
        var post = await posts.CreateAsync(a.Id, new CreatePostRequest("Riêng tư", "OnlyMe", null, null));

        var result = await likes.LikeAsync(stranger.Id, post.Value!.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.PostNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Comment_ReplyDepthIsLimitedToOne()
    {
        var (db, a, b) = await CreateFriendPairAsync();
        var posts = new PostService(db);
        var comments = new CommentService(db);
        var post = await posts.CreateAsync(a.Id, new CreatePostRequest("Bài", "Public", null, null));

        var root = await comments.CreateAsync(b.Id, post.Value!.Id, new CreateCommentRequest("Gốc", null));
        Assert.True(root.Succeeded);

        var reply = await comments.CreateAsync(a.Id, post.Value.Id, new CreateCommentRequest("Trả lời", root.Value!.Id));
        Assert.True(reply.Succeeded);

        var tooDeep = await comments.CreateAsync(b.Id, post.Value.Id, new CreateCommentRequest("Cấp 2", reply.Value!.Id));
        Assert.False(tooDeep.Succeeded);
        Assert.Equal(ErrorCodes.CommentReplyDepthExceeded, tooDeep.ErrorCode);

        var reloadedPost = await posts.GetByIdAsync(a.Id, post.Value.Id);
        Assert.Equal(2, reloadedPost.Value!.CommentCount);

        var roots = await comments.GetPostCommentsAsync(a.Id, post.Value.Id, null, 20);
        var rootItem = Assert.Single(roots.Value!.Items);
        Assert.Equal(1, rootItem.ReplyCount);

        var replies = await comments.GetRepliesAsync(a.Id, root.Value.Id, null, 20);
        Assert.Single(replies.Value!.Items);
    }

    [Fact]
    public async Task DeleteRootComment_AlsoHidesReplies()
    {
        var (db, a, b) = await CreateFriendPairAsync();
        var posts = new PostService(db);
        var comments = new CommentService(db);
        var post = await posts.CreateAsync(a.Id, new CreatePostRequest("Bài", "Public", null, null));
        var root = await comments.CreateAsync(b.Id, post.Value!.Id, new CreateCommentRequest("Gốc", null));
        await comments.CreateAsync(a.Id, post.Value.Id, new CreateCommentRequest("Trả lời", root.Value!.Id));

        var deleted = await comments.DeleteAsync(b.Id, root.Value.Id);
        Assert.True(deleted.Succeeded);

        // Bình luận gốc đã xóa → không còn danh sách trả lời để xem (404 COMMENT_NOT_FOUND).
        var replies = await comments.GetRepliesAsync(a.Id, root.Value.Id, null, 20);
        Assert.False(replies.Succeeded);
        Assert.Equal(ErrorCodes.CommentNotFound, replies.ErrorCode);

        // Các trả lời phải bị xóa mềm theo bình luận gốc.
        var replyRows = await db.Comments
            .AsNoTracking()
            .Where(c => c.ParentCommentId == root.Value.Id)
            .ToListAsync();

        Assert.NotEmpty(replyRows);
        Assert.All(replyRows, reply => Assert.NotNull(reply.DeletedAt));

        var reloadedPost = await posts.GetByIdAsync(a.Id, post.Value.Id);
        Assert.Equal(0, reloadedPost.Value!.CommentCount);
    }

    [Fact]
    public async Task Share_IncrementsOriginalShareCount_AndIncludesSharedPost()
    {
        var (db, a, b) = await CreateFriendPairAsync();
        var posts = new PostService(db);
        var original = await posts.CreateAsync(a.Id, new CreatePostRequest("Gốc", "Public", null, null));

        var shared = await posts.CreateAsync(b.Id, new CreatePostRequest(null, "Public", null, original.Value!.Id));

        Assert.True(shared.Succeeded);
        Assert.NotNull(shared.Value!.SharedPost);
        Assert.Equal(original.Value.Id, shared.Value.SharedPost!.Id);

        var reloaded = await posts.GetByIdAsync(a.Id, original.Value.Id);
        Assert.Equal(1, reloaded.Value!.ShareCount);
    }

    [Fact]
    public async Task UpdatePost_ByNonAuthor_IsForbidden()
    {
        var (db, a, b) = await CreateFriendPairAsync();
        var posts = new PostService(db);
        var post = await posts.CreateAsync(a.Id, new CreatePostRequest("Bài", "Public", null, null));

        var result = await posts.UpdateAsync(b.Id, post.Value!.Id, new UpdatePostRequest("Sửa trộm", null));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task UserPosts_RespectVisibility()
    {
        var (db, a, b) = await CreateFriendPairAsync();
        var stranger = await AddStrangerAsync(db);
        var posts = new PostService(db);
        await posts.CreateAsync(a.Id, new CreatePostRequest("Công khai", "Public", null, null));
        await posts.CreateAsync(a.Id, new CreatePostRequest("Bạn bè", "Friends", null, null));

        var friendView = await posts.GetUserPostsAsync(b.Id, a.Id, null, 20);
        Assert.Equal(2, friendView.Value!.Items.Count);

        var strangerView = await posts.GetUserPostsAsync(stranger.Id, a.Id, null, 20);
        Assert.Single(strangerView.Value!.Items);
        Assert.Equal("Công khai", strangerView.Value.Items[0].Content);
    }
}
