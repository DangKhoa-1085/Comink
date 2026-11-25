// Cài đặt nghiệp vụ quản trị: khóa/mở khóa tài khoản (Giai đoạn 5).
// Nghiệp vụ nằm ở Application; ghi dữ liệu qua IIdentityService, thu hồi phiên qua ITokenService,
// phát sự kiện qua IAccountNotifier (cài đặt SignalR ở tầng Api).
using Comink.Application.Admin.Abstractions;
using Comink.Application.Common;
using Comink.Application.Identity.Abstractions;
using Comink.Domain.Admin;
using Microsoft.Extensions.Logging;

namespace Comink.Application.Admin;

/// <summary>Luồng khóa/mở khóa tài khoản cho quản trị viên.</summary>
public class AdminService : IAdminService
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly IAccountNotifier _accountNotifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IIdentityService identityService,
        ITokenService tokenService,
        IAccountNotifier accountNotifier,
        TimeProvider timeProvider,
        ILogger<AdminService> logger)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _accountNotifier = accountNotifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OperationResult<AdminUserData>> LockUserAsync(
        Guid adminId,
        Guid targetUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var user = await _identityService.GetByIdAsync(targetUserId, cancellationToken);
        if (user is null)
        {
            return OperationResult<AdminUserData>.Failure(ErrorCodes.UserNotFound);
        }

        var reasonText = AdminUserRules.NormalizeLockReason(reason);

        // Phòng thủ nhiều lớp: endpoint đã validate bằng FluentValidation, nhưng service cũng phải
        // từ chối lý do quá dài để không làm hỏng giao dịch ở tầng database (varchar(500)).
        if (reasonText is { Length: > AdminUserRules.MaxLockReasonLength })
        {
            return OperationResult<AdminUserData>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["reason"] =
                [
                    $"Lý do khóa tối đa {AdminUserRules.MaxLockReasonLength} ký tự."
                ]
            });
        }

        var action = AdminUserRules.EvaluateLock(adminId, targetUserId, user.AccountStatus);
        if (action == AdminAccountAction.CannotLockSelf)
        {
            return OperationResult<AdminUserData>.Failure(ErrorCodes.AdminCannotLockSelf);
        }

        // Đã khóa rồi: trả thành công nhưng không ghi đè lý do/thời điểm khóa ban đầu (idempotent).
        if (action == AdminAccountAction.AlreadyInState)
        {
            return OperationResult<AdminUserData>.Success(ToAdminUserData(user));
        }

        var now = _timeProvider.GetUtcNow();
        var updated = await _identityService.SetAccountStatusAsync(
            targetUserId,
            AdminUserRules.LockedStatus,
            reasonText,
            adminId,
            cancellationToken);

        if (!updated.Succeeded)
        {
            return OperationResult<AdminUserData>.Failure(MapIdentityFailure(updated.Error));
        }

        // Thu hồi token TRƯỚC khi phát sự kiện: chặn việc dùng refresh token trong khoảng thời gian
        // giữa hai bước (bẫy số 3 trong docs/tasks/phase5_admin.md mục 6).
        var revokedTokens = await _tokenService.RevokeAllForUserAsync(targetUserId, cancellationToken);

        await _accountNotifier.AccountLockedAsync(targetUserId, cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} đã khóa tài khoản {TargetUserId}; thu hồi {RevokedTokenCount} refresh token",
            adminId,
            targetUserId,
            revokedTokens);

        return OperationResult<AdminUserData>.Success(
            ToAdminUserData(user, AdminUserRules.LockedStatus, reasonText, now));
    }

    /// <inheritdoc />
    public async Task<OperationResult<AdminUserData>> UnlockUserAsync(
        Guid adminId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _identityService.GetByIdAsync(targetUserId, cancellationToken);
        if (user is null)
        {
            return OperationResult<AdminUserData>.Failure(ErrorCodes.UserNotFound);
        }

        if (AdminUserRules.EvaluateUnlock(user.AccountStatus) == AdminAccountAction.AlreadyInState)
        {
            return OperationResult<AdminUserData>.Success(ToAdminUserData(user));
        }

        var updated = await _identityService.SetAccountStatusAsync(
            targetUserId,
            AdminUserRules.ActiveStatus,
            reason: null,
            changedBy: adminId,
            cancellationToken);

        if (!updated.Succeeded)
        {
            return OperationResult<AdminUserData>.Failure(MapIdentityFailure(updated.Error));
        }

        _logger.LogInformation(
            "Admin {AdminId} đã mở khóa tài khoản {TargetUserId}",
            adminId,
            targetUserId);

        return OperationResult<AdminUserData>.Success(
            ToAdminUserData(user, AdminUserRules.ActiveStatus, lockedReason: null, lockedAt: null));
    }

    /// <summary>Ánh xạ lỗi của kho tài khoản sang mã lỗi API.</summary>
    private static string MapIdentityFailure(IdentityFailure failure) => failure switch
    {
        IdentityFailure.NotFound => ErrorCodes.UserNotFound,
        _ => ErrorCodes.InternalError
    };

    private static AdminUserData ToAdminUserData(IdentityUserData user) =>
        new(user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            user.AccountStatus,
            user.LockedReason,
            user.LockedAt,
            user.CreatedAt);

    private static AdminUserData ToAdminUserData(
        IdentityUserData user,
        string accountStatus,
        string? lockedReason,
        DateTimeOffset? lockedAt) =>
        new(user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            accountStatus,
            lockedReason,
            lockedAt,
            user.CreatedAt);
}
