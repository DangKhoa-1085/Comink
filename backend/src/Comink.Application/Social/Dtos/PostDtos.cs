// DTO cho bài viết, media và bình luận (docs/05_api_contract.md mục 7.4–7.6).
namespace Comink.Application.Social.Dtos;

/// <summary>Ảnh đính kèm của bài viết.</summary>
public record MediaDto(Guid Id, string Url, string? ThumbnailUrl, int? Width, int? Height);

/// <summary>Bài viết trả về cho client.</summary>
public record PostDto(
    Guid Id,
    UserSummaryDto Author,
    string? Content,
    string Visibility,
    IReadOnlyList<MediaDto> Media,
    PostDto? SharedPost,
    int LikeCount,
    int CommentCount,
    int ShareCount,
    bool LikedByMe,
    bool IsMine,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Bình luận của bài viết.</summary>
public record CommentDto(
    Guid Id,
    Guid PostId,
    Guid? ParentCommentId,
    UserSummaryDto Author,
    string Content,
    int ReplyCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Yêu cầu tạo bài viết.</summary>
public record CreatePostRequest(
    string? Content,
    string? Visibility,
    IReadOnlyList<Guid>? MediaIds,
    Guid? SharedPostId);

/// <summary>Yêu cầu sửa bài viết; trường bỏ trống thì giữ nguyên.</summary>
public record UpdatePostRequest(string? Content, string? Visibility);

/// <summary>Yêu cầu tạo bình luận / trả lời.</summary>
public record CreateCommentRequest(string Content, Guid? ParentCommentId);

/// <summary>Yêu cầu sửa bình luận.</summary>
public record UpdateCommentRequest(string Content);

/// <summary>Giới hạn nghiệp vụ của nhóm bài viết.</summary>
public static class PostLimits
{
    /// <summary>Độ dài tối đa của nội dung bài viết.</summary>
    public const int MaxContentLength = 5000;

    /// <summary>Số ảnh tối đa cho một bài.</summary>
    public const int MaxMediaPerPost = 10;

    /// <summary>Tên phạm vi hiển thị hợp lệ.</summary>
    public static readonly string[] VisibilityValues = ["Public", "Friends", "OnlyMe"];
}
