// Entity ánh xạ bảng `post_likes` (docs/03_database_design.md mục 3.2).
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>Một người thích một bài viết (khóa chính kép, xóa cứng).</summary>
public class PostLike
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
