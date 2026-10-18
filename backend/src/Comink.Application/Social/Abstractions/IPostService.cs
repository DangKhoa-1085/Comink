// Hợp đồng nghiệp vụ bài viết, bảng tin (docs/05_api_contract.md mục 7.4).
using Comink.Application.Common;
using Comink.Application.Social.Dtos;

namespace Comink.Application.Social.Abstractions;

/// <summary>Nghiệp vụ bài viết và bảng tin.</summary>
public interface IPostService
{
    /// <summary>Tạo bài viết (có thể kèm ảnh hoặc là bài chia sẻ).</summary>
    Task<OperationResult<PostDto>> CreateAsync(
        Guid currentUserId,
        CreatePostRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Xem một bài; không có quyền xem thì trả POST_NOT_FOUND.</summary>
    Task<OperationResult<PostDto>> GetByIdAsync(
        Guid currentUserId,
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>Sửa bài của chính mình.</summary>
    Task<OperationResult<PostDto>> UpdateAsync(
        Guid currentUserId,
        Guid postId,
        UpdatePostRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Xóa mềm bài của chính mình.</summary>
    Task<OperationResult<bool>> DeleteAsync(
        Guid currentUserId,
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>Bảng tin: bài của bạn bè, người đang theo dõi và của chính mình.</summary>
    Task<OperationResult<PagedList<PostDto>>> GetFeedAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Bài trên trang cá nhân của một người (lọc theo quyền xem).</summary>
    Task<OperationResult<PagedList<PostDto>>> GetUserPostsAsync(
        Guid currentUserId,
        Guid targetUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>Nghiệp vụ thích bài viết.</summary>
public interface ILikeService
{
    /// <summary>Thích bài (idempotent).</summary>
    Task<OperationResult<bool>> LikeAsync(
        Guid currentUserId,
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>Bỏ thích (idempotent).</summary>
    Task<OperationResult<bool>> UnlikeAsync(
        Guid currentUserId,
        Guid postId,
        CancellationToken cancellationToken = default);
}

/// <summary>Nghiệp vụ bình luận (chỉ một cấp trả lời).</summary>
public interface ICommentService
{
    /// <summary>Tạo bình luận hoặc trả lời.</summary>
    Task<OperationResult<CommentDto>> CreateAsync(
        Guid currentUserId,
        Guid postId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Bình luận gốc của một bài (thứ tự cũ trước).</summary>
    Task<OperationResult<PagedList<CommentDto>>> GetPostCommentsAsync(
        Guid currentUserId,
        Guid postId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Các trả lời của một bình luận gốc (thứ tự cũ trước).</summary>
    Task<OperationResult<PagedList<CommentDto>>> GetRepliesAsync(
        Guid currentUserId,
        Guid commentId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Sửa bình luận của chính mình.</summary>
    Task<OperationResult<CommentDto>> UpdateAsync(
        Guid currentUserId,
        Guid commentId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Xóa mềm bình luận (tác giả bình luận hoặc chủ bài viết).</summary>
    Task<OperationResult<bool>> DeleteAsync(
        Guid currentUserId,
        Guid commentId,
        CancellationToken cancellationToken = default);
}
