// Hợp đồng nghiệp vụ bạn bè hai chiều (docs/tasks/phase2_social.md mục 3.2).
using Comink.Application.Common;
using Comink.Application.Social.Dtos;

namespace Comink.Application.Social.Abstractions;

/// <summary>Nghiệp vụ kết bạn hai chiều.</summary>
public interface IFriendshipService
{
    /// <summary>
    /// Gửi lời mời kết bạn. Idempotent: nếu đã có lời mời cùng chiều thì trả lại lời mời đó;
    /// nếu đối phương đã mời trước thì tự động chấp nhận.
    /// </summary>
    Task<OperationResult<FriendshipDto>> SendRequestAsync(
        Guid currentUserId,
        Guid addresseeId,
        CancellationToken cancellationToken = default);

    /// <summary>Chấp nhận lời mời (chỉ người nhận).</summary>
    Task<OperationResult<FriendshipDto>> AcceptAsync(
        Guid currentUserId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>Từ chối lời mời nhận được, hoặc thu hồi lời mời đã gửi (xóa dòng).</summary>
    Task<OperationResult<bool>> RemoveRequestAsync(
        Guid currentUserId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>Hủy kết bạn (một trong hai người).</summary>
    Task<OperationResult<bool>> RemoveFriendAsync(
        Guid currentUserId,
        Guid friendUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách bạn bè.</summary>
    Task<OperationResult<PagedList<FriendshipDto>>> GetFriendsAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Lời mời tôi nhận được.</summary>
    Task<OperationResult<PagedList<FriendshipDto>>> GetIncomingRequestsAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Lời mời tôi đã gửi.</summary>
    Task<OperationResult<PagedList<FriendshipDto>>> GetOutgoingRequestsAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}
