// Truy vấn dùng chung cho module mạng xã hội — giữ số truy vấn cố định, tránh N+1.
using Comink.Application.Social.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Tiện ích truy vấn dùng chung cho bạn bè, theo dõi và tìm kiếm người dùng.</summary>
internal static class SocialLookup
{
    /// <summary>Trạng thái tài khoản được phép xuất hiện trong kết quả tìm kiếm/danh sách.</summary>
    private const string ActiveAccountStatus = "Active";

    /// <summary>
    /// Tính quan hệ giữa người xem và một tập người dùng bằng **3 truy vấn cố định**
    /// (bạn bè, tôi đang theo dõi, họ đang theo dõi tôi) dù tập có bao nhiêu phần tử.
    /// </summary>
    public static async Task<Dictionary<Guid, RelationshipDto>> GetRelationshipsAsync(
        ApplicationDbContext db,
        Guid viewerId,
        IReadOnlyCollection<Guid> targetIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, RelationshipDto>();
        if (targetIds.Count == 0)
        {
            return result;
        }

        var ids = targetIds.ToList();

        var friendships = await db.Friendships
            .AsNoTracking()
            .Where(f =>
                (f.RequesterId == viewerId && ids.Contains(f.AddresseeId)) ||
                (f.AddresseeId == viewerId && ids.Contains(f.RequesterId)))
            .Select(f => new { f.RequesterId, f.AddresseeId, f.Status })
            .ToListAsync(cancellationToken);

        var followingIds = await db.Follows
            .AsNoTracking()
            .Where(f => f.FollowerId == viewerId && ids.Contains(f.FolloweeId))
            .Select(f => f.FolloweeId)
            .ToListAsync(cancellationToken);

        var followedByIds = await db.Follows
            .AsNoTracking()
            .Where(f => f.FolloweeId == viewerId && ids.Contains(f.FollowerId))
            .Select(f => f.FollowerId)
            .ToListAsync(cancellationToken);

        var followingSet = followingIds.ToHashSet();
        var followedBySet = followedByIds.ToHashSet();

        foreach (var targetId in ids)
        {
            var friendship = friendships.FirstOrDefault(f => f.RequesterId == targetId || f.AddresseeId == targetId);
            var relation = FriendshipRelation.None;

            if (friendship is not null)
            {
                relation = friendship.Status == FriendshipStatus.Accepted
                    ? FriendshipRelation.Friends
                    : friendship.RequesterId == viewerId
                        ? FriendshipRelation.OutgoingPending
                        : FriendshipRelation.IncomingPending;
            }

            result[targetId] = new RelationshipDto(
                relation,
                followingSet.Contains(targetId),
                followedBySet.Contains(targetId));
        }

        return result;
    }

    /// <summary>Hai người đã là bạn bè (đã chấp nhận) hay chưa.</summary>
    public static Task<bool> AreFriendsAsync(
        ApplicationDbContext db,
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken) =>
        db.Friendships
            .AsNoTracking()
            .AnyAsync(
                f => f.Status == FriendshipStatus.Accepted &&
                     ((f.RequesterId == firstUserId && f.AddresseeId == secondUserId) ||
                      (f.RequesterId == secondUserId && f.AddresseeId == firstUserId)),
                cancellationToken);

    /// <summary>Người dùng tồn tại và đang hoạt động (chưa bị khóa).</summary>
    public static Task<bool> IsActiveUserAsync(
        ApplicationDbContext db,
        Guid userId,
        CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.AccountStatus == ActiveAccountStatus, cancellationToken);

    /// <summary>
    /// Tải thông tin rút gọn của nhiều người dùng (1 truy vấn) và tính quan hệ với người xem,
    /// trả về **theo đúng thứ tự** của <paramref name="userIds"/>.
    /// </summary>
    public static async Task<List<UserSummaryDto>> GetUserSummariesAsync(
        ApplicationDbContext db,
        Guid viewerId,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var ids = userIds.ToList();

        var rows = await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.UserName,
                DisplayName = u.Profile != null ? u.Profile.DisplayName : string.Empty,
                AvatarUrl = u.Profile != null ? u.Profile.AvatarUrl : null,
                Bio = u.Profile != null ? u.Profile.Bio : null
            })
            .ToListAsync(cancellationToken);

        var byId = rows.ToDictionary(r => r.Id);
        var relationships = await GetRelationshipsAsync(db, viewerId, ids, cancellationToken);

        var result = new List<UserSummaryDto>(ids.Count);
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var row))
            {
                continue;
            }

            result.Add(new UserSummaryDto(
                row.Id,
                row.UserName ?? string.Empty,
                row.DisplayName ?? string.Empty,
                row.AvatarUrl,
                row.Bio,
                relationships[id]));
        }

        return result;
    }
}
