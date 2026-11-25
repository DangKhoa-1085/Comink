// Ánh xạ bài viết sang DTO bằng số truy vấn cố định cho cả một trang (tránh N+1).
using Comink.Application.Social.Dtos;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Chuyển danh sách bài viết sang DTO chỉ với 5 truy vấn cho cả trang.</summary>
internal static class PostMapping
{
    public static async Task<List<PostDto>> ToDtosAsync(
        ApplicationDbContext db,
        Guid viewerId,
        IReadOnlyList<Post> posts,
        CancellationToken cancellationToken)
    {
        if (posts.Count == 0)
        {
            return [];
        }

        // 1) Bài được chia sẻ (chỉ một cấp)
        var sharedIds = posts
            .Where(p => p.SharedPostId is not null)
            .Select(p => p.SharedPostId!.Value)
            .Distinct()
            .ToList();

        var sharedPosts = sharedIds.Count == 0
            ? []
            : await db.Posts
                .AsNoTracking()
                .Where(p => sharedIds.Contains(p.Id) && p.DeletedAt == null)
                .ToListAsync(cancellationToken);

        var sharedById = sharedPosts.ToDictionary(p => p.Id);

        // 2) Tác giả của bài chính và bài chia sẻ — 1 truy vấn
        var authorIds = posts.Select(p => p.AuthorId)
            .Concat(sharedPosts.Select(p => p.AuthorId))
            .Distinct()
            .ToList();

        var authors = await SocialLookup.GetUserSummariesAsync(db, viewerId, authorIds, cancellationToken);
        var authorById = authors.ToDictionary(a => a.Id);

        // 3) Ảnh của tất cả bài trong trang — 1 truy vấn
        var allPostIds = posts.Select(p => p.Id)
            .Concat(sharedPosts.Select(p => p.Id))
            .Distinct()
            .ToList();

        var mediaRows = await db.PostMediaLinks
            .AsNoTracking()
            .Where(pm => allPostIds.Contains(pm.PostId))
            .Join(
                db.MediaAssets.AsNoTracking(),
                pm => pm.MediaId,
                m => m.Id,
                (pm, m) => new { pm.PostId, pm.SortOrder, m.Id, m.Url, m.ThumbnailUrl, m.Width, m.Height })
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);

        var mediaByPost = mediaRows
            .GroupBy(row => row.PostId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MediaDto>)g
                    .Select(row => new MediaDto(row.Id, row.Url, row.ThumbnailUrl, row.Width, row.Height))
                    .ToList());

        // 4) Người xem đã thích bài nào — 1 truy vấn
        var likedPostIds = await db.PostLikes
            .AsNoTracking()
            .Where(l => l.UserId == viewerId && allPostIds.Contains(l.PostId))
            .Select(l => l.PostId)
            .ToListAsync(cancellationToken);

        var likedSet = likedPostIds.ToHashSet();

        // 5) Ghép DTO (không truy vấn thêm)
        var result = new List<PostDto>(posts.Count);
        foreach (var post in posts)
        {
            PostDto? sharedDto = null;
            if (post.SharedPostId is not null && sharedById.TryGetValue(post.SharedPostId.Value, out var shared))
            {
                sharedDto = Build(shared, authorById, mediaByPost, likedSet, viewerId, sharedDto: null);
            }

            result.Add(Build(post, authorById, mediaByPost, likedSet, viewerId, sharedDto));
        }

        return result;
    }

    private static PostDto Build(
        Post post,
        IReadOnlyDictionary<Guid, UserSummaryDto> authorById,
        IReadOnlyDictionary<Guid, IReadOnlyList<MediaDto>> mediaByPost,
        HashSet<Guid> likedSet,
        Guid viewerId,
        PostDto? sharedDto)
    {
        var author = authorById.TryGetValue(post.AuthorId, out var found)
            ? found
            : new UserSummaryDto(
                post.AuthorId,
                string.Empty,
                string.Empty,
                null,
                null,
                new RelationshipDto(FriendshipRelation.None, false, false));

        return new PostDto(
            post.Id,
            author,
            post.Content,
            post.Visibility.ToString(),
            mediaByPost.TryGetValue(post.Id, out var media) ? media : [],
            sharedDto,
            post.LikeCount,
            post.CommentCount,
            post.ShareCount,
            likedSet.Contains(post.Id),
            post.AuthorId == viewerId,
            post.CreatedAt,
            post.UpdatedAt);
    }
}
