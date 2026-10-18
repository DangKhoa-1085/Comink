// DTO của nhóm mạng xã hội GĐ 2 — hợp đồng với web/app (docs/tasks/phase2_social.md mục 3).
using Comink.Application.Common;

namespace Comink.Application.Social.Dtos;

/// <summary>Giá trị hợp lệ của <see cref="RelationshipDto.Friendship"/>.</summary>
public static class FriendshipRelation
{
    public const string None = "None";
    public const string OutgoingPending = "OutgoingPending";
    public const string IncomingPending = "IncomingPending";
    public const string Friends = "Friends";
}

/// <summary>Quan hệ giữa người xem và một người dùng khác (để web hiện đúng nút).</summary>
public record RelationshipDto(string Friendship, bool Following, bool FollowedBy);

/// <summary>Thông tin rút gọn của một người dùng, dùng trong danh sách.</summary>
public record UserSummaryDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    RelationshipDto Relationship);

/// <summary>Hồ sơ công khai của một người dùng.</summary>
public record PublicProfileDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? CoverUrl,
    string? Location,
    int FollowerCount,
    int FollowingCount,
    int FriendCount,
    DateTimeOffset CreatedAt,
    RelationshipDto Relationship);

/// <summary>Một dòng quan hệ bạn bè (lời mời hoặc đã là bạn).</summary>
public record FriendshipDto(
    Guid Id,
    UserSummaryDto User,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);

/// <summary>Yêu cầu gửi lời mời kết bạn.</summary>
public record SendFriendRequestRequest(Guid AddresseeId);

/// <summary>Kết quả một trang danh sách người dùng.</summary>
public static class SocialPaging
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    /// <summary>Chuẩn hóa kích thước trang theo hợp đồng (mặc định 20, tối đa 50).</summary>
    public static int Normalize(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

    /// <summary>Tạo trang kết quả từ danh sách lấy dư một phần tử.</summary>
    public static PagedList<T> Build<T>(IReadOnlyList<T> fetched, int pageSize, Func<T, (DateTimeOffset CreatedAt, Guid Id)> cursorOf)
    {
        var hasMore = fetched.Count > pageSize;
        var items = hasMore ? fetched.Take(pageSize).ToList() : fetched.ToList();
        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(cursorOf(items[^1]).CreatedAt, cursorOf(items[^1]).Id)
            : null;
        return new PagedList<T>(items, nextCursor, hasMore);
    }
}
