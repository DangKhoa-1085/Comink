// Kiểm tra quyền xem bài viết dùng chung cho bài viết, thích và bình luận.
using Comink.Domain.Social;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>
/// Mọi đường truy cập nội dung (xem bài, thích, bình luận, chia sẻ) phải đi qua đây
/// để không có đường nào bỏ sót quyền xem (AGENTS.md mục 9.2).
/// </summary>
internal static class PostAccess
{
    /// <summary>Người xem có được xem bài này không.</summary>
    public static async Task<bool> CanViewAsync(
        ApplicationDbContext db,
        Guid viewerId,
        Post post,
        CancellationToken cancellationToken)
    {
        if (!PostVisibilityRules.IsVisible(post.Status, post.DeletedAt))
        {
            return false;
        }

        if (post.AuthorId == viewerId)
        {
            return true;
        }

        var areFriends = post.Visibility == Domain.Enums.Visibility.Friends
            && await SocialLookup.AreFriendsAsync(db, viewerId, post.AuthorId, cancellationToken);

        return PostVisibilityRules.CanView(post.Visibility, isAuthor: false, areFriends);
    }

    /// <summary>
    /// Tìm bài mà người xem được phép xem. Trả null nếu bài không tồn tại, đã xóa
    /// hoặc người xem không có quyền (ba trường hợp này cố ý gộp làm một — trả 404).
    /// </summary>
    public static async Task<Post?> FindVisiblePostAsync(
        ApplicationDbContext db,
        Guid viewerId,
        Guid postId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = db.Posts.AsQueryable();
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        var post = await query.FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken);
        if (post is null)
        {
            return null;
        }

        return await CanViewAsync(db, viewerId, post, cancellationToken) ? post : null;
    }
}
