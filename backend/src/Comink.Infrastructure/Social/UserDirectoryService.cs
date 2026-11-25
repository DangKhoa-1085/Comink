// Tìm người dùng (gõ không dấu) và hồ sơ công khai.
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Tìm kiếm người dùng theo tên hiển thị/tên đăng nhập, không phân biệt dấu tiếng Việt.</summary>
public class UserDirectoryService : IUserDirectoryService
{
    private const string ActiveAccountStatus = "Active";

    private readonly ApplicationDbContext _db;

    public UserDirectoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<UserSummaryDto>>> SearchAsync(
        Guid currentUserId,
        string search,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var term = search.Trim().ToLowerInvariant();
        if (term.Length == 0)
        {
            return OperationResult<PagedList<UserSummaryDto>>.Failure(ErrorCodes.ValidationFailed);
        }

        var pageSize = SocialPaging.Normalize(limit);
        var pattern = $"%{term}%";
        var decoded = CursorCodec.TryDecode(cursor);

        var query = from u in _db.Users.AsNoTracking()
                    join p in _db.UserProfiles.AsNoTracking() on u.Id equals p.UserId
                    where u.AccountStatus == ActiveAccountStatus
                          && u.Id != currentUserId
                          // Tài khoản Admin là tài khoản ẩn: không xuất hiện trong danh bạ với bất kỳ ai
                          // (kể cả admin khác) — quản lý ở /api/admin/users.
                          && !AdminAccounts.Ids(_db).Contains(u.Id)
                    select new
                    {
                        u.Id,
                        u.UserName,
                        u.CreatedAt,
                        p.DisplayName,
                        p.AvatarUrl,
                        p.Bio
                    };

        // So khớp không dấu: bỏ dấu tên hiển thị rồi LIKE (dùng chỉ mục trigram ở M1).
        query = query.Where(x =>
            EF.Functions.Like(CominkDbFunctions.ImmutableUnaccent(x.DisplayName.ToLower()), pattern) ||
            EF.Functions.Like(x.UserName!.ToLower(), pattern));

        // Phân trang con trỏ (created_at, id) giảm dần — không dùng OFFSET.
        if (decoded is not null)
        {
            var cursorCreatedAt = decoded.Value.CreatedAt;
            var cursorId = decoded.Value.Id;
            query = query.Where(x =>
                x.CreatedAt < cursorCreatedAt ||
                (x.CreatedAt == cursorCreatedAt && x.Id.CompareTo(cursorId) < 0));
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageRows = hasMore ? rows.Take(pageSize).ToList() : rows;
        var relationships = await SocialLookup.GetRelationshipsAsync(
            _db,
            currentUserId,
            pageRows.Select(r => r.Id).ToList(),
            cancellationToken);

        var items = pageRows
            .Select(r => new UserSummaryDto(
                r.Id,
                r.UserName ?? string.Empty,
                r.DisplayName,
                r.AvatarUrl,
                r.Bio,
                relationships[r.Id]))
            .ToList();

        var nextCursor = hasMore && pageRows.Count > 0
            ? CursorCodec.Encode(pageRows[^1].CreatedAt, pageRows[^1].Id)
            : null;

        return OperationResult<PagedList<UserSummaryDto>>.Success(
            new PagedList<UserSummaryDto>(items, nextCursor, hasMore));
    }

    /// <inheritdoc />
    public async Task<OperationResult<PublicProfileDto>> GetPublicProfileAsync(
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(
                u => u.Id == targetUserId && u.AccountStatus == ActiveAccountStatus,
                cancellationToken);

        if (user is null)
        {
            return OperationResult<PublicProfileDto>.Failure(ErrorCodes.NotFound);
        }

        // Tài khoản Admin ẩn với người dùng thường: trả 404 như không tồn tại (không dùng 403
        // để không tiết lộ tài khoản). Admin khác và chính họ vẫn xem được.
        if (!await AdminAccounts.IsVisibleToAsync(_db, currentUserId, targetUserId, cancellationToken))
        {
            return OperationResult<PublicProfileDto>.Failure(ErrorCodes.NotFound);
        }

        var relationships = await SocialLookup.GetRelationshipsAsync(
            _db,
            currentUserId,
            [targetUserId],
            cancellationToken);

        var profile = user.Profile;

        return OperationResult<PublicProfileDto>.Success(new PublicProfileDto(
            user.Id,
            user.UserName ?? string.Empty,
            profile?.DisplayName ?? string.Empty,
            string.IsNullOrWhiteSpace(profile?.Bio) ? null : profile!.Bio,
            profile?.AvatarUrl,
            profile?.CoverUrl,
            profile?.Location,
            profile?.FollowerCount ?? 0,
            profile?.FollowingCount ?? 0,
            profile?.FriendCount ?? 0,
            user.CreatedAt,
            relationships[targetUserId]));
    }
}
