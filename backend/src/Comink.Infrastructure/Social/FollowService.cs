// Nghiệp vụ theo dõi một chiều (docs/tasks/phase2_social.md mục 3.3).
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using Comink.Domain.Social;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Theo dõi / bỏ theo dõi và danh sách người theo dõi.</summary>
public class FollowService : IFollowService
{
    private readonly ApplicationDbContext _db;

    public FollowService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> FollowAsync(
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (FriendshipRules.IsSelfPair(currentUserId, targetUserId))
        {
            return OperationResult<bool>.Failure(ErrorCodes.SocialCannotFollowSelf);
        }

        if (!await SocialLookup.IsActiveUserAsync(_db, targetUserId, cancellationToken))
        {
            return OperationResult<bool>.Failure(ErrorCodes.NotFound);
        }

        var alreadyFollowing = await _db.Follows
            .AsNoTracking()
            .AnyAsync(f => f.FollowerId == currentUserId && f.FolloweeId == targetUserId, cancellationToken);

        if (alreadyFollowing)
        {
            return OperationResult<bool>.Success(true);
        }

        _db.Follows.Add(new Follow { FollowerId = currentUserId, FolloweeId = targetUserId });
        await AdjustFollowCountsAsync(currentUserId, targetUserId, +1, cancellationToken);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Đã theo dõi do một request khác vừa tạo (khóa chính kép chặn bản ghi trùng).
            return OperationResult<bool>.Success(true);
        }

        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> UnfollowAsync(
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (FriendshipRules.IsSelfPair(currentUserId, targetUserId))
        {
            return OperationResult<bool>.Failure(ErrorCodes.SocialCannotFollowSelf);
        }

        var follow = await _db.Follows.FirstOrDefaultAsync(
            f => f.FollowerId == currentUserId && f.FolloweeId == targetUserId,
            cancellationToken);

        if (follow is null)
        {
            return OperationResult<bool>.Success(true);
        }

        _db.Follows.Remove(follow);
        await AdjustFollowCountsAsync(currentUserId, targetUserId, -1, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public Task<OperationResult<PagedList<UserSummaryDto>>> GetFollowersAsync(
        Guid currentUserId,
        Guid targetUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetListAsync(currentUserId, targetUserId, followers: true, cursor, limit, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<PagedList<UserSummaryDto>>> GetFollowingAsync(
        Guid currentUserId,
        Guid targetUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetListAsync(currentUserId, targetUserId, followers: false, cursor, limit, cancellationToken);

    private async Task<OperationResult<PagedList<UserSummaryDto>>> GetListAsync(
        Guid currentUserId,
        Guid targetUserId,
        bool followers,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await SocialLookup.IsActiveUserAsync(_db, targetUserId, cancellationToken))
        {
            return OperationResult<PagedList<UserSummaryDto>>.Failure(ErrorCodes.NotFound);
        }

        var pageSize = SocialPaging.Normalize(limit);
        var decoded = CursorCodec.TryDecode(cursor);

        // Bảng follows không có cột id riêng: con trỏ dùng (created_at, user_id của phía còn lại).
        var query = _db.Follows.AsNoTracking();
        query = followers
            ? query.Where(f => f.FolloweeId == targetUserId)
            : query.Where(f => f.FollowerId == targetUserId);

        if (decoded is not null)
        {
            var cursorCreatedAt = decoded.Value.CreatedAt;
            var cursorUserId = decoded.Value.Id;
            query = query.Where(f =>
                f.CreatedAt < cursorCreatedAt ||
                (f.CreatedAt == cursorCreatedAt
                 && (followers ? f.FollowerId : f.FolloweeId).CompareTo(cursorUserId) < 0));
        }

        var rows = await query
            .OrderByDescending(f => f.CreatedAt)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageRows = hasMore ? rows.Take(pageSize).ToList() : rows;
        var userIds = pageRows
            .Select(f => followers ? f.FollowerId : f.FolloweeId)
            .ToList();

        var items = await SocialLookup.GetUserSummariesAsync(_db, currentUserId, userIds, cancellationToken);

        var nextCursor = hasMore && pageRows.Count > 0
            ? CursorCodec.Encode(
                pageRows[^1].CreatedAt,
                followers ? pageRows[^1].FollowerId : pageRows[^1].FolloweeId)
            : null;

        return OperationResult<PagedList<UserSummaryDto>>.Success(
            new PagedList<UserSummaryDto>(items, nextCursor, hasMore));
    }

    private async Task AdjustFollowCountsAsync(
        Guid followerId,
        Guid followeeId,
        int delta,
        CancellationToken cancellationToken)
    {
        var profiles = await _db.UserProfiles
            .Where(p => p.UserId == followerId || p.UserId == followeeId)
            .ToListAsync(cancellationToken);

        foreach (var profile in profiles)
        {
            // Người theo dõi: tăng/giảm following_count; người được theo dõi: follower_count.
            if (profile.UserId == followerId)
            {
                profile.FollowingCount = delta > 0
                    ? FriendshipRules.Increment(profile.FollowingCount)
                    : FriendshipRules.Decrement(profile.FollowingCount);
            }

            if (profile.UserId == followeeId)
            {
                profile.FollowerCount = delta > 0
                    ? FriendshipRules.Increment(profile.FollowerCount)
                    : FriendshipRules.Decrement(profile.FollowerCount);
            }
        }
    }
}
