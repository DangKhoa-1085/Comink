// Entity ánh xạ bảng `comments` (docs/03_database_design.md mục 3.2).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Bình luận của bài viết. Chỉ cho phép **một cấp trả lời**: khi
/// <see cref="ParentCommentId"/> khác null thì bình luận cha phải là bình luận gốc
/// (kiểm tra ở tầng ứng dụng). Xóa mềm bằng <see cref="DeletedAt"/>.
/// </summary>
public class Comment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public Guid AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;

    /// <summary>Bình luận gốc được trả lời; null nếu đây là bình luận gốc.</summary>
    public Guid? ParentCommentId { get; set; }
    public Comment? Parent { get; set; }

    /// <summary>Các trả lời trực tiếp (tối đa một cấp).</summary>
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();

    public string Content { get; set; } = string.Empty;

    public ContentStatus Status { get; set; } = ContentStatus.Active;

    /// <summary>Số trả lời trực tiếp (chỉ dùng cho bình luận gốc).</summary>
    public int ReplyCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
