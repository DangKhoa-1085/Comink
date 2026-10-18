// Entity ánh xạ bảng nối `post_media` (docs/03_database_design.md mục 3.2).
namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>Ảnh của bài viết, có thứ tự hiển thị.</summary>
public class PostMedia
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public Guid MediaId { get; set; }
    public MediaAsset Media { get; set; } = null!;

    /// <summary>Thứ tự hiển thị trong bài.</summary>
    public short SortOrder { get; set; }
}
