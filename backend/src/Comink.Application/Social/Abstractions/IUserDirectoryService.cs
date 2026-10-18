// Hợp đồng tìm kiếm người dùng và xem hồ sơ công khai.
using Comink.Application.Common;
using Comink.Application.Social.Dtos;

namespace Comink.Application.Social.Abstractions;

/// <summary>Tìm người dùng (gõ không dấu vẫn khớp) và xem hồ sơ công khai.</summary>
public interface IUserDirectoryService
{
    /// <summary>
    /// Tìm người dùng theo tên hiển thị hoặc tên đăng nhập.
    /// Kết quả sắp theo (created_at, id) giảm dần để phân trang con trỏ thống nhất.
    /// </summary>
    Task<OperationResult<PagedList<UserSummaryDto>>> SearchAsync(
        Guid currentUserId,
        string search,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Hồ sơ công khai của một người dùng (không tồn tại hoặc bị khóa → NOT_FOUND).</summary>
    Task<OperationResult<PublicProfileDto>> GetPublicProfileAsync(
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}
