// Nghiệp vụ thích / bỏ thích bài viết (docs/05_api_contract.md mục 7.5).
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Domain.Social;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Thích và bỏ thích bài viết, giữ cột đếm đồng bộ.</summary>
public class LikeService : ILikeService
{
    private readonly ApplicationDbContext _db;

    public LikeService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> LikeAsync(
        Guid currentUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await PostAccess.FindVisiblePostAsync(_db, currentUserId, postId, tracked: true, cancellationToken);
        if (post is null)
        {
            return OperationResult<bool>.Failure(ErrorCodes.PostNotFound);
        }

        var exists = await _db.PostLikes
            .AsNoTracking()
            .AnyAsync(l => l.PostId == postId && l.UserId == currentUserId, cancellationToken);

        // Idempotent: thích lại không tăng đếm lần nữa.
        if (exists)
        {
            return OperationResult<bool>.Success(true);
        }

        _db.PostLikes.Add(new PostLike { PostId = postId, UserId = currentUserId });
        post.LikeCount = FriendshipRules.Increment(post.LikeCount);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hai request thích cùng lúc: khóa chính kép chặn bản ghi trùng.
            return OperationResult<bool>.Success(true);
        }

        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> UnlikeAsync(
        Guid currentUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await PostAccess.FindVisiblePostAsync(_db, currentUserId, postId, tracked: true, cancellationToken);
        if (post is null)
        {
            return OperationResult<bool>.Failure(ErrorCodes.PostNotFound);
        }

        var like = await _db.PostLikes
            .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == currentUserId, cancellationToken);

        if (like is null)
        {
            return OperationResult<bool>.Success(true);
        }

        _db.PostLikes.Remove(like);
        post.LikeCount = FriendshipRules.Decrement(post.LikeCount);
        await _db.SaveChangesAsync(cancellationToken);

        return OperationResult<bool>.Success(true);
    }
}
