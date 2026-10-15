// Cổng sinh token và quản lý vòng đời refresh token.
// Application sở hữu luồng nghiệp vụ; chi tiết JWT và lưu trữ nằm ở Infrastructure.
namespace Comink.Application.Identity.Abstractions;

/// <summary>Access token vừa sinh kèm thời điểm hết hạn.</summary>
public record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Refresh token dạng thô (chỉ trả cho client một lần) kèm thời điểm hết hạn.
/// Trong database chỉ lưu băm của token này.
/// </summary>
public record RefreshTokenResult(string Token, DateTimeOffset ExpiresAt, Guid FamilyId);

/// <summary>
/// Kết quả xoay vòng refresh token.
/// </summary>
public enum RefreshOutcome
{
    /// <summary>Xoay vòng thành công.</summary>
    Rotated = 0,

    /// <summary>Token không tồn tại, hết hạn hoặc đã bị thu hồi.</summary>
    Invalid,

    /// <summary>Phát hiện dùng lại token cũ của một họ đã bị thu hồi; đã thu hồi toàn bộ họ.</summary>
    Reused
}

/// <summary>Kết quả xoay vòng refresh token.</summary>
public record RefreshRotationResult(
    RefreshOutcome Outcome,
    Guid UserId,
    RefreshTokenResult? NewToken);

/// <summary>
/// Hợp đồng sinh access token (JWT) và quản lý refresh token xoay vòng.
/// </summary>
public interface ITokenService
{
    /// <summary>Thời hạn của access token tính bằng phút (dùng để thông báo cho client).</summary>
    int AccessTokenLifetimeMinutes { get; }

    /// <summary>Sinh access token cho người dùng.</summary>
    AccessTokenResult CreateAccessToken(Guid userId, string userName, IEnumerable<string> roles);

    /// <summary>
    /// Phát refresh token mới cho một phiên đăng nhập.
    /// <paramref name="familyId"/> null nghĩa là bắt đầu họ mới (đăng nhập mới).
    /// </summary>
    Task<RefreshTokenResult> IssueRefreshTokenAsync(
        Guid userId,
        Guid? familyId,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xoay vòng refresh token: thu hồi token cũ và phát token mới cùng họ.
    /// Nếu phát hiện token cũ đã bị thu hồi trước đó thì thu hồi toàn bộ họ và trả <see cref="RefreshOutcome.Reused"/>.
    /// </summary>
    Task<RefreshRotationResult> RotateRefreshTokenAsync(
        string refreshToken,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>Thu hồi một refresh token (đăng xuất). Không báo lỗi nếu token không tồn tại.</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
