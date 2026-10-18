// Entity ánh xạ bảng `posts` (docs/03_database_design.md mục 3.2).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Bài viết. Xóa mềm bằng <see cref="DeletedAt"/>; bài chia sẻ có <see cref="SharedPostId"/>.
/// </summary>
public class Post
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;

    /// <summary>Nội dung; được phép rỗng nếu có ảnh hoặc là bài chia sẻ (tối đa 5000 ký tự).</summary>
    public string? Content { get; set; }

    public Visibility Visibility { get; set; } = Visibility.Friends;

    /// <summary>Bài gốc được chia sẻ; null nếu là bài thường.</summary>
    public Guid? SharedPostId { get; set; }
    public Post? SharedPost { get; set; }

    /// <summary>Trạng thái kiểm duyệt do quản trị viên đặt.</summary>
    public ContentStatus Status { get; set; } = ContentStatus.Active;

    // Cột đếm denormalized, cập nhật trong cùng transaction với thao tác tương ứng
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Ảnh đính kèm, sắp theo <see cref="PostMedia.SortOrder"/>.</summary>
    public ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();

    /// <summary>
    /// Concurrency token ánh xạ cột hệ thống `xmin` của PostgreSQL
    /// (docs/03_database_design.md mục 1.2) — không tạo cột mới trong DB.
    /// </summary>
    public uint Version { get; set; }
}
