// Entity ánh xạ bảng `follows` (docs/03_database_design.md mục 3.2).
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Theo dõi một chiều: <see cref="FollowerId"/> theo dõi <see cref="FolloweeId"/>.
/// Bảng nối nên dùng khóa chính kép và xóa cứng.
/// </summary>
public class Follow
{
    public Guid FollowerId { get; set; }
    public ApplicationUser Follower { get; set; } = null!;

    public Guid FolloweeId { get; set; }
    public ApplicationUser Followee { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
