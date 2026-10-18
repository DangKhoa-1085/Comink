// Nghiệp vụ kết bạn hai chiều (docs/tasks/phase2_social.md mục 3.2).
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using Comink.Domain.Enums;
using Comink.Domain.Social;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Gửi / chấp nhận / từ chối lời mời kết bạn, hủy kết bạn và danh sách bạn bè.</summary>
public class FriendshipService : IFriendshipService
{
    private const string ModeFriends = "friends";
    private const string ModeIncoming = "incoming";
    private const string ModeOutgoing = "outgoing";

    private readonly ApplicationDbContext _db;

    public FriendshipService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<OperationResult<FriendshipDto>> SendRequestAsync(
        Guid currentUserId,
        Guid addresseeId,
        CancellationToken cancellationToken = default)
    {
        if (FriendshipRules.IsSelfPair(currentUserId, addresseeId))
        {
            return OperationResult<FriendshipDto>.Failure(ErrorCodes.SocialCannotFriendSelf);
        }

        if (!await SocialLookup.IsActiveUserAsync(_db, addresseeId, cancellationToken))
        {
            return OperationResult<FriendshipDto>.Failure(ErrorCodes.NotFound);
        }

        var existing = await FindBetweenAsync(currentUserId, addresseeId, tracked: true, cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
            {
                return OperationResult<FriendshipDto>.Failure(ErrorCodes.SocialAlreadyFriends);
            }

            // Đã gửi lời mời trước đó → trả lại lời mời cũ (idempotent).
            if (existing.RequesterId == currentUserId)
            {
                return OperationResult<FriendshipDto>.Success(
                    await ToDtoAsync(existing, currentUserId, cancellationToken));
            }

            // Đối phương đã mời mình trước → tự động chấp nhận.
            existing.Status = FriendshipStatus.Accepted;
            existing.RespondedAt = DateTimeOffset.UtcNow;
            await AdjustFriendCountsAsync(existing.RequesterId, existing.AddresseeId, +1, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            return OperationResult<FriendshipDto>.Success(
                await ToDtoAsync(existing, currentUserId, cancellationToken));
        }

        var friendship = new Friendship
        {
            RequesterId = currentUserId,
            AddresseeId = addresseeId,
            Status = FriendshipStatus.Pending
        };

        _db.Friendships.Add(friendship);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hai người bấm cùng lúc: ràng buộc ux_friendships_pair đã chặn bản ghi thứ hai.
            _db.Entry(friendship).State = EntityState.Detached;

            var raced = await FindBetweenAsync(currentUserId, addresseeId, tracked: false, cancellationToken);
            if (raced is null)
            {
                return OperationResult<FriendshipDto>.Failure(ErrorCodes.InternalError);
            }

            return raced.Status == FriendshipStatus.Accepted
                ? OperationResult<FriendshipDto>.Failure(ErrorCodes.SocialAlreadyFriends)
                : OperationResult<FriendshipDto>.Success(await ToDtoAsync(raced, currentUserId, cancellationToken));
        }

        return OperationResult<FriendshipDto>.Success(
            await ToDtoAsync(friendship, currentUserId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<OperationResult<FriendshipDto>> AcceptAsync(
        Guid currentUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var friendship = await _db.Friendships
            .FirstOrDefaultAsync(f => f.Id == requestId, cancellationToken);

        if (friendship is null)
        {
            return OperationResult<FriendshipDto>.Failure(ErrorCodes.SocialFriendRequestNotFound);
        }

        if (friendship.AddresseeId != currentUserId)
        {
            return OperationResult<FriendshipDto>.Failure(ErrorCodes.SocialNotRequestAddressee);
        }

        // Đã chấp nhận rồi → idempotent.
        if (friendship.Status == FriendshipStatus.Accepted)
        {
            return OperationResult<FriendshipDto>.Success(
                await ToDtoAsync(friendship, currentUserId, cancellationToken));
        }

        if (!FriendshipRules.CanAccept(friendship.Status))
        {
            return OperationResult<FriendshipDto>.Failure(ErrorCodes.SocialFriendRequestNotFound);
        }

        friendship.Status = FriendshipStatus.Accepted;
        friendship.RespondedAt = DateTimeOffset.UtcNow;
        await AdjustFriendCountsAsync(friendship.RequesterId, friendship.AddresseeId, +1, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return OperationResult<FriendshipDto>.Success(
            await ToDtoAsync(friendship, currentUserId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> RemoveRequestAsync(
        Guid currentUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var friendship = await _db.Friendships
            .FirstOrDefaultAsync(f => f.Id == requestId, cancellationToken);

        if (friendship is null)
        {
            return OperationResult<bool>.Failure(ErrorCodes.SocialFriendRequestNotFound);
        }

        if (friendship.RequesterId != currentUserId && friendship.AddresseeId != currentUserId)
        {
            return OperationResult<bool>.Failure(ErrorCodes.Forbidden);
        }

        // Đã là bạn bè thì không còn là "lời mời" để từ chối/thu hồi.
        if (friendship.Status == FriendshipStatus.Accepted)
        {
            return OperationResult<bool>.Failure(ErrorCodes.SocialFriendRequestNotFound);
        }

        _db.Friendships.Remove(friendship);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> RemoveFriendAsync(
        Guid currentUserId,
        Guid friendUserId,
        CancellationToken cancellationToken = default)
    {
        if (FriendshipRules.IsSelfPair(currentUserId, friendUserId))
        {
            return OperationResult<bool>.Success(true);
        }

        var friendship = await FindBetweenAsync(currentUserId, friendUserId, tracked: true, cancellationToken);

        // Không phải bạn bè → coi như đã hủy (idempotent).
        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
        {
            return OperationResult<bool>.Success(true);
        }

        _db.Friendships.Remove(friendship);
        await AdjustFriendCountsAsync(currentUserId, friendUserId, -1, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public Task<OperationResult<PagedList<FriendshipDto>>> GetFriendsAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetListAsync(currentUserId, ModeFriends, cursor, limit, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<PagedList<FriendshipDto>>> GetIncomingRequestsAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetListAsync(currentUserId, ModeIncoming, cursor, limit, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<PagedList<FriendshipDto>>> GetOutgoingRequestsAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetListAsync(currentUserId, ModeOutgoing, cursor, limit, cancellationToken);

    private async Task<OperationResult<PagedList<FriendshipDto>>> GetListAsync(
        Guid currentUserId,
        string mode,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var pageSize = SocialPaging.Normalize(limit);
        var decoded = CursorCodec.TryDecode(cursor);

        var query = _db.Friendships.AsNoTracking();

        query = mode switch
        {
            ModeFriends => query.Where(f => f.Status == FriendshipStatus.Accepted &&
                                            (f.RequesterId == currentUserId || f.AddresseeId == currentUserId)),
            ModeIncoming => query.Where(f => f.Status == FriendshipStatus.Pending && f.AddresseeId == currentUserId),
            _ => query.Where(f => f.Status == FriendshipStatus.Pending && f.RequesterId == currentUserId)
        };

        if (decoded is not null)
        {
            var cursorCreatedAt = decoded.Value.CreatedAt;
            var cursorId = decoded.Value.Id;
            query = query.Where(f =>
                f.CreatedAt < cursorCreatedAt ||
                (f.CreatedAt == cursorCreatedAt && f.Id.CompareTo(cursorId) < 0));
        }

        var rows = await query
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageRows = hasMore ? rows.Take(pageSize).ToList() : rows;

        var otherUserIds = pageRows
            .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
            .ToList();

        var summaries = await SocialLookup.GetUserSummariesAsync(_db, currentUserId, otherUserIds, cancellationToken);
        var summaryById = summaries.ToDictionary(s => s.Id);

        var items = new List<FriendshipDto>(pageRows.Count);
        foreach (var friendship in pageRows)
        {
            var otherUserId = friendship.RequesterId == currentUserId ? friendship.AddresseeId : friendship.RequesterId;
            if (!summaryById.TryGetValue(otherUserId, out var other))
            {
                continue;
            }

            items.Add(new FriendshipDto(
                friendship.Id,
                other,
                friendship.Status.ToString(),
                friendship.CreatedAt,
                friendship.RespondedAt));
        }

        var nextCursor = hasMore && pageRows.Count > 0
            ? CursorCodec.Encode(pageRows[^1].CreatedAt, pageRows[^1].Id)
            : null;

        return OperationResult<PagedList<FriendshipDto>>.Success(
            new PagedList<FriendshipDto>(items, nextCursor, hasMore));
    }

    private Task<Friendship?> FindBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = tracked ? _db.Friendships : _db.Friendships.AsNoTracking();

        return query.FirstOrDefaultAsync(
            f => (f.RequesterId == firstUserId && f.AddresseeId == secondUserId) ||
                 (f.RequesterId == secondUserId && f.AddresseeId == firstUserId),
            cancellationToken);
    }

    private async Task AdjustFriendCountsAsync(
        Guid firstUserId,
        Guid secondUserId,
        int delta,
        CancellationToken cancellationToken)
    {
        var profiles = await _db.UserProfiles
            .Where(p => p.UserId == firstUserId || p.UserId == secondUserId)
            .ToListAsync(cancellationToken);

        foreach (var profile in profiles)
        {
            profile.FriendCount = delta > 0
                ? FriendshipRules.Increment(profile.FriendCount)
                : FriendshipRules.Decrement(profile.FriendCount);
        }
    }

    private async Task<FriendshipDto> ToDtoAsync(
        Friendship friendship,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        var otherUserId = friendship.RequesterId == viewerId ? friendship.AddresseeId : friendship.RequesterId;
        var summaries = await SocialLookup.GetUserSummariesAsync(_db, viewerId, [otherUserId], cancellationToken);

        var other = summaries.Count > 0
            ? summaries[0]
            : new UserSummaryDto(
                otherUserId,
                string.Empty,
                string.Empty,
                null,
                null,
                new RelationshipDto(FriendshipRelation.None, false, false));

        return new FriendshipDto(
            friendship.Id,
            other,
            friendship.Status.ToString(),
            friendship.CreatedAt,
            friendship.RespondedAt);
    }
}
