// Nghiệp vụ bài viết và bảng tin (docs/05_api_contract.md mục 7.4).
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using Comink.Domain.Enums;
using Comink.Domain.Social;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Tạo / xem / sửa / xóa bài viết và bảng tin.</summary>
public class PostService : IPostService
{
    private readonly ApplicationDbContext _db;

    public PostService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<OperationResult<PostDto>> CreateAsync(
        Guid currentUserId,
        CreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        var content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim();
        var mediaIds = request.MediaIds?.Distinct().ToList() ?? [];
        var sharedPostId = request.SharedPostId;

        if (mediaIds.Count > PostLimits.MaxMediaPerPost)
        {
            return OperationResult<PostDto>.Failure(ErrorCodes.PostTooManyMedia);
        }

        Visibility visibility;
        if (string.IsNullOrWhiteSpace(request.Visibility))
        {
            // Bỏ trống thì lấy phạm vi mặc định trong cài đặt người dùng (M0).
            var defaultVisibility = await _db.UserSettings
                .AsNoTracking()
                .Where(s => s.UserId == currentUserId)
                .Select(s => s.DefaultPostVisibility)
                .FirstOrDefaultAsync(cancellationToken);

            visibility = ParseVisibility(defaultVisibility) ?? Visibility.Friends;
        }
        else
        {
            var parsed = ParseVisibility(request.Visibility);
            if (parsed is null)
            {
                return OperationResult<PostDto>.Failure(ErrorCodes.ValidationFailed);
            }

            visibility = parsed.Value;
        }

        // Bài rỗng chỉ hợp lệ khi có ảnh hoặc là bài chia sẻ.
        if (content is null && mediaIds.Count == 0 && sharedPostId is null)
        {
            return OperationResult<PostDto>.Failure(ErrorCodes.PostContentRequired);
        }

        if (mediaIds.Count > 0)
        {
            var ownedCount = await _db.MediaAssets
                .AsNoTracking()
                .CountAsync(
                    m => mediaIds.Contains(m.Id)
                         && m.OwnerId == currentUserId
                         && m.Context == MediaContext.Post
                         && m.DeletedAt == null,
                    cancellationToken);

            if (ownedCount != mediaIds.Count)
            {
                return OperationResult<PostDto>.Failure(ErrorCodes.PostMediaNotOwned);
            }
        }

        Post? sharedPost = null;
        if (sharedPostId is not null)
        {
            sharedPost = await PostAccess.FindVisiblePostAsync(
                _db, currentUserId, sharedPostId.Value, tracked: true, cancellationToken);

            if (sharedPost is null)
            {
                return OperationResult<PostDto>.Failure(ErrorCodes.PostNotFound);
            }
        }

        var post = new Post
        {
            AuthorId = currentUserId,
            Content = content,
            Visibility = visibility,
            SharedPostId = sharedPostId
        };

        short sortOrder = 0;
        foreach (var mediaId in mediaIds)
        {
            post.Media.Add(new PostMedia { MediaId = mediaId, SortOrder = sortOrder++ });
        }

        _db.Posts.Add(post);

        if (sharedPost is not null)
        {
            sharedPost.ShareCount = FriendshipRules.Increment(sharedPost.ShareCount);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(currentUserId, post.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PostDto>> GetByIdAsync(
        Guid currentUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await PostAccess.FindVisiblePostAsync(_db, currentUserId, postId, tracked: false, cancellationToken);
        if (post is null)
        {
            return OperationResult<PostDto>.Failure(ErrorCodes.PostNotFound);
        }

        var dtos = await PostMapping.ToDtosAsync(_db, currentUserId, [post], cancellationToken);
        return OperationResult<PostDto>.Success(dtos[0]);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PostDto>> UpdateAsync(
        Guid currentUserId,
        Guid postId,
        UpdatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken);
        if (post is null)
        {
            return OperationResult<PostDto>.Failure(ErrorCodes.PostNotFound);
        }

        if (post.AuthorId != currentUserId)
        {
            return OperationResult<PostDto>.Failure(ErrorCodes.Forbidden);
        }

        if (request.Visibility is not null)
        {
            var parsed = ParseVisibility(request.Visibility);
            if (parsed is null)
            {
                return OperationResult<PostDto>.Failure(ErrorCodes.ValidationFailed);
            }

            post.Visibility = parsed.Value;
        }

        if (request.Content is not null)
        {
            var content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim();
            if (content is null)
            {
                var hasMedia = await _db.PostMediaLinks.AnyAsync(pm => pm.PostId == post.Id, cancellationToken);
                if (!hasMedia && post.SharedPostId is null)
                {
                    return OperationResult<PostDto>.Failure(ErrorCodes.PostContentRequired);
                }
            }

            post.Content = content;
        }

        post.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(currentUserId, post.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> DeleteAsync(
        Guid currentUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken);
        if (post is null)
        {
            return OperationResult<bool>.Failure(ErrorCodes.PostNotFound);
        }

        if (post.AuthorId != currentUserId)
        {
            return OperationResult<bool>.Failure(ErrorCodes.Forbidden);
        }

        post.DeletedAt = DateTimeOffset.UtcNow;

        if (post.SharedPostId is not null)
        {
            var original = await _db.Posts
                .FirstOrDefaultAsync(p => p.Id == post.SharedPostId.Value, cancellationToken);

            if (original is not null)
            {
                original.ShareCount = FriendshipRules.Decrement(original.ShareCount);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<PostDto>>> GetFeedAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var pageSize = SocialPaging.Normalize(limit);
        var decoded = CursorCodec.TryDecode(cursor);

        // Đối tượng của bảng tin: bạn bè + người đang theo dõi + chính mình
        // (docs/03_database_design.md mục 5.3).
        var friendIds = _db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted && f.RequesterId == currentUserId)
            .Select(f => f.AddresseeId)
            .Union(_db.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted && f.AddresseeId == currentUserId)
                .Select(f => f.RequesterId));

        var followingIds = _db.Follows
            .Where(f => f.FollowerId == currentUserId)
            .Select(f => f.FolloweeId);

        var query = _db.Posts
            .AsNoTracking()
            .Where(p => p.DeletedAt == null && p.Status == ContentStatus.Active)
            .Where(p => p.AuthorId == currentUserId
                        || friendIds.Contains(p.AuthorId)
                        || followingIds.Contains(p.AuthorId))
            .Where(p => p.AuthorId == currentUserId
                        || p.Visibility == Visibility.Public
                        || (p.Visibility == Visibility.Friends && friendIds.Contains(p.AuthorId)));

        if (decoded is not null)
        {
            var cursorCreatedAt = decoded.Value.CreatedAt;
            var cursorId = decoded.Value.Id;
            query = query.Where(p =>
                p.CreatedAt < cursorCreatedAt ||
                (p.CreatedAt == cursorCreatedAt && p.Id.CompareTo(cursorId) < 0));
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        return await BuildPageAsync(currentUserId, rows, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<PostDto>>> GetUserPostsAsync(
        Guid currentUserId,
        Guid targetUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await SocialLookup.IsActiveUserAsync(_db, targetUserId, cancellationToken))
        {
            return OperationResult<PagedList<PostDto>>.Failure(ErrorCodes.NotFound);
        }

        var pageSize = SocialPaging.Normalize(limit);
        var decoded = CursorCodec.TryDecode(cursor);
        var areFriends = await SocialLookup.AreFriendsAsync(_db, currentUserId, targetUserId, cancellationToken);
        var isSelf = currentUserId == targetUserId;

        var query = _db.Posts
            .AsNoTracking()
            .Where(p => p.AuthorId == targetUserId && p.DeletedAt == null && p.Status == ContentStatus.Active)
            .Where(p => isSelf
                        || p.Visibility == Visibility.Public
                        || (p.Visibility == Visibility.Friends && areFriends));

        if (decoded is not null)
        {
            var cursorCreatedAt = decoded.Value.CreatedAt;
            var cursorId = decoded.Value.Id;
            query = query.Where(p =>
                p.CreatedAt < cursorCreatedAt ||
                (p.CreatedAt == cursorCreatedAt && p.Id.CompareTo(cursorId) < 0));
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        return await BuildPageAsync(currentUserId, rows, pageSize, cancellationToken);
    }

    private async Task<OperationResult<PagedList<PostDto>>> BuildPageAsync(
        Guid currentUserId,
        List<Post> rows,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var hasMore = rows.Count > pageSize;
        var pagePosts = hasMore ? rows.Take(pageSize).ToList() : rows;

        var items = await PostMapping.ToDtosAsync(_db, currentUserId, pagePosts, cancellationToken);

        var nextCursor = hasMore && pagePosts.Count > 0
            ? CursorCodec.Encode(pagePosts[^1].CreatedAt, pagePosts[^1].Id)
            : null;

        return OperationResult<PagedList<PostDto>>.Success(
            new PagedList<PostDto>(items, nextCursor, hasMore));
    }

    private static Visibility? ParseVisibility(string? value) => value switch
    {
        "Public" => Visibility.Public,
        "Friends" => Visibility.Friends,
        "OnlyMe" => Visibility.OnlyMe,
        _ => null
    };
}
