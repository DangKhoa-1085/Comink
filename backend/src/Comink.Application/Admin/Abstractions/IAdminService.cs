// Nghiệp vụ quản trị tài khoản: khóa / mở khóa (Giai đoạn 5).
using Comink.Application.Admin.Abstractions;
using Comink.Application.Common;
using Comink.Application.Identity.Abstractions;
using Comink.Domain.Admin;

namespace Comink.Application.Admin.Abstractions;

/// <summary>Luồng nghiệp vụ quản trị tài khoản (khóa/mở khóa).</summary>
public interface IAdminService
{
    /// <summary>
    /// Khóa tài khoản. Từ chối nếu admin tự khóa chính mình. Khi khóa thành công: thu hồi toàn bộ
    /// refresh token và ngắt kết nối SignalR để phiên hiện tại mất hiệu lực ngay.
    /// </summary>
    Task<OperationResult<AdminUserData>> LockUserAsync(
        Guid adminId,
        Guid targetUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>Mở khóa tài khoản (idempotent khi tài khoản đang hoạt động).</summary>
    Task<OperationResult<AdminUserData>> UnlockUserAsync(
        Guid adminId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}
