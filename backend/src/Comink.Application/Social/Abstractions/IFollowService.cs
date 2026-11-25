// Hợp đồng nghiệp vụ theo dõi một chiều (docs/tasks/phase2_social.md mục 3.3).
using Comink.Application.Common;
using Comink.Application.Social.Dtos;

namespace Comink.Application.Social.Abstractions;

/// <summary>Nghiệp vụ theo dõi một chiều.</summary>
public interface IFollowService
{
    /// <summary>Theo dõi một người (idempotent).</summary>
    Task<OperationResult<bool>> FollowAsync(
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Bỏ theo dõi một người (idempotent).</summary>
    Task<OperationResult<bool>> UnfollowAsync(
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách người theo dõi một người dùng.</summary>
    Task<OperationResult<PagedList<UserSummaryDto>>> GetFollowersAsync(
        Guid currentUserId,
        Guid targetUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách người mà một người dùng đang theo dõi.</summary>
    Task<OperationResult<PagedList<UserSummaryDto>>> GetFollowingAsync(
        Guid currentUserId,
        Guid targetUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}
