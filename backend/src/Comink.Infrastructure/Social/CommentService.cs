// Nghiệp vụ bình luận — chỉ một cấp trả lời (docs/05_api_contract.md mục 7.5).
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using Comink.Domain.Social;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Tạo / sửa / xóa bình luận và danh sách trả lời.</summary>
public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _db;

    public CommentService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<OperationResult<CommentDto>> CreateAsync(
        Guid currentUserId,
        Guid postId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await PostAccess.FindVisiblePostAsync(_db, currentUserId, postId, tracked: true, cancellationToken);
        if (post is null)
        {
            return OperationResult<CommentDto>.Failure(ErrorCodes.PostNotFound);
        }

        Comment? parent = null;
        if (request.ParentCommentId is not null)
        {
            parent = await _db.Comments.FirstOrDefaultAsync(
                c => c.Id == request.ParentCommentId.Value && c.DeletedAt == null,
                cancellationToken);

            if (parent is null || parent.PostId != postId)
            {
                return OperationResult<CommentDto>.Failure(ErrorCodes.CommentNotFound);
            }

            if (!CommentRules.CanReplyTo(parent.ParentCommentId))
            {
                return OperationResult<CommentDto>.Failure(ErrorCodes.CommentReplyDepthExceeded);
            }
        }

        var comment = new Comment
        {
            PostId = postId,
            AuthorId = currentUserId,
            ParentCommentId = parent?.Id,
            Content = request.Content.Trim()
        };

        _db.Comments.Add(comment);
        post.CommentCount = FriendshipRules.Increment(post.CommentCount);

        if (parent is not null)
        {
            parent.ReplyCount = FriendshipRules.Increment(parent.ReplyCount);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dtos = await ToDtosAsync([comment], currentUserId, cancellationToken);
        return OperationResult<CommentDto>.Success(dtos[0]);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<CommentDto>>> GetPostCommentsAsync(
        Guid currentUserId,
        Guid postId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var post = await PostAccess.FindVisiblePostAsync(_db, currentUserId, postId, tracked: false, cancellationToken);
        if (post is null)
        {
            return OperationResult<PagedList<CommentDto>>.Failure(ErrorCodes.PostNotFound);
        }

        var query = _db.Comments
            .AsNoTracking()
            .Where(c => c.PostId == postId && c.DeletedAt == null && c.ParentCommentId == null);

        return await ReadPageAsync(currentUserId, query, cursor, limit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<CommentDto>>> GetRepliesAsync(
        Guid currentUserId,
        Guid commentId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var root = await _db.Comments
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancellationToken);

        if (root is null)
        {
            return OperationResult<PagedList<CommentDto>>.Failure(ErrorCodes.CommentNotFound);
        }

        var post = await PostAccess.FindVisiblePostAsync(_db, currentUserId, root.PostId, tracked: false, cancellationToken);
        if (post is null)
        {
            return OperationResult<PagedList<CommentDto>>.Failure(ErrorCodes.PostNotFound);
        }

        var query = _db.Comments
            .AsNoTracking()
            .Where(c => c.ParentCommentId == commentId && c.DeletedAt == null);

        return await ReadPageAsync(currentUserId, query, cursor, limit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<CommentDto>> UpdateAsync(
        Guid currentUserId,
        Guid commentId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var comment = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancellationToken);

        if (comment is null)
        {
            return OperationResult<CommentDto>.Failure(ErrorCodes.CommentNotFound);
        }

        if (comment.AuthorId != currentUserId)
        {
            return OperationResult<CommentDto>.Failure(ErrorCodes.Forbidden);
        }

        comment.Content = request.Content.Trim();
        comment.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var dtos = await ToDtosAsync([comment], currentUserId, cancellationToken);
        return OperationResult<CommentDto>.Success(dtos[0]);
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> DeleteAsync(
        Guid currentUserId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancellationToken);

        if (comment is null)
        {
            return OperationResult<bool>.Failure(ErrorCodes.CommentNotFound);
        }

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == comment.PostId, cancellationToken);

        // Tác giả bình luận hoặc chủ bài viết được xóa.
        if (comment.AuthorId != currentUserId && post?.AuthorId != currentUserId)
        {
            return OperationResult<bool>.Failure(ErrorCodes.Forbidden);
        }

        var now = DateTimeOffset.UtcNow;
        comment.DeletedAt = now;

        if (post is not null)
        {
            post.CommentCount = FriendshipRules.Decrement(post.CommentCount);
        }

        if (comment.ParentCommentId is null)
        {
            // Xóa bình luận gốc thì các trả lời cũng bị coi là đã xóa.
            var replies = await _db.Comments
                .Where(c => c.ParentCommentId == comment.Id && c.DeletedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var reply in replies)
            {
                reply.DeletedAt = now;
                if (post is not null)
                {
                    post.CommentCount = FriendshipRules.Decrement(post.CommentCount);
                }
            }

            comment.ReplyCount = 0;
        }
        else
        {
            var parent = await _db.Comments
                .FirstOrDefaultAsync(c => c.Id == comment.ParentCommentId!.Value, cancellationToken);

            if (parent is not null)
            {
                parent.ReplyCount = FriendshipRules.Decrement(parent.ReplyCount);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    private async Task<OperationResult<PagedList<CommentDto>>> ReadPageAsync(
        Guid currentUserId,
        IQueryable<Comment> query,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var pageSize = SocialPaging.Normalize(limit);
        var decoded = CursorCodec.TryDecode(cursor);

        // Bình luận hiển thị theo thứ tự cũ trước, nên con trỏ đi theo chiều tăng.
        if (decoded is not null)
        {
            var cursorCreatedAt = decoded.Value.CreatedAt;
            var cursorId = decoded.Value.Id;
            query = query.Where(c =>
                c.CreatedAt > cursorCreatedAt ||
                (c.CreatedAt == cursorCreatedAt && c.Id.CompareTo(cursorId) > 0));
        }

        var rows = await query
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageRows = hasMore ? rows.Take(pageSize).ToList() : rows;

        var items = await ToDtosAsync(pageRows, currentUserId, cancellationToken);

        var nextCursor = hasMore && pageRows.Count > 0
            ? CursorCodec.Encode(pageRows[^1].CreatedAt, pageRows[^1].Id)
            : null;

        return OperationResult<PagedList<CommentDto>>.Success(
            new PagedList<CommentDto>(items, nextCursor, hasMore));
    }

    private async Task<List<CommentDto>> ToDtosAsync(
        IReadOnlyList<Comment> comments,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (comments.Count == 0)
        {
            return [];
        }

        var authorIds = comments.Select(c => c.AuthorId).Distinct().ToList();
        var authors = await SocialLookup.GetUserSummariesAsync(_db, currentUserId, authorIds, cancellationToken);
        var authorById = authors.ToDictionary(a => a.Id);

        return comments
            .Select(comment =>
            {
                var author = authorById.TryGetValue(comment.AuthorId, out var found)
                    ? found
                    : new UserSummaryDto(
                        comment.AuthorId,
                        string.Empty,
                        string.Empty,
                        null,
                        null,
                        new RelationshipDto(FriendshipRelation.None, false, false));

                return new CommentDto(
                    comment.Id,
                    comment.PostId,
                    comment.ParentCommentId,
                    author,
                    comment.Content,
                    comment.ReplyCount,
                    comment.CreatedAt,
                    comment.UpdatedAt);
            })
            .ToList();
    }
}
