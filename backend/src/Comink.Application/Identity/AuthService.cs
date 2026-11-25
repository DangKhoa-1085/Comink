// Nghiệp vụ xác thực: đăng ký, đăng nhập, làm mới token, đăng xuất.
// Toàn bộ luồng nằm ở Application; chi tiết Identity và JWT đi qua cổng IIdentityService / ITokenService.
using Comink.Application.Common;
using Comink.Application.Identity.Abstractions;
using Comink.Application.Identity.Dtos;

namespace Comink.Application.Identity;

/// <summary>
/// Dịch vụ xác thực. Được đăng ký ở DI và gọi từ endpoint trong tầng Api.
/// </summary>
public class AuthService
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;

    public AuthService(IIdentityService identityService, ITokenService tokenService)
    {
        _identityService = identityService;
        _tokenService = tokenService;
    }

    /// <summary>Đăng ký tài khoản mới và trả về cặp token.</summary>
    public async Task<OperationResult<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        RefreshTokenContext context,
        CancellationToken cancellationToken = default)
    {
        var created = await _identityService.CreateUserAsync(
            request.UserName,
            request.Email,
            request.Password,
            request.DisplayName,
            cancellationToken);

        if (!created.Succeeded)
        {
            return OperationResult<AuthResponse>.Failure(created.Error switch
            {
                IdentityFailure.DuplicateEmail => ErrorCodes.AuthEmailAlreadyExists,
                IdentityFailure.DuplicateUserName => ErrorCodes.AuthUserNameAlreadyExists,
                IdentityFailure.PasswordPolicyViolation => ErrorCodes.ValidationFailed,
                _ => ErrorCodes.InternalError
            });
        }

        // Đăng ký xong đăng nhập luôn: dùng lại đúng luồng đăng nhập để tránh lệch hành vi.
        return await LoginAsync(
            new LoginRequest(request.Email, request.Password, context.DeviceName),
            context,
            cancellationToken);
    }

    /// <summary>Đăng nhập bằng email hoặc tên đăng nhập.</summary>
    public async Task<OperationResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        RefreshTokenContext context,
        CancellationToken cancellationToken = default)
    {
        var userId = await _identityService.ValidateCredentialsAsync(
            request.EmailOrUserName,
            request.Password,
            cancellationToken);

        // Sai tài khoản và sai mật khẩu trả cùng một mã lỗi để không tiết lộ tài khoản có tồn tại.
        if (userId is null)
        {
            return OperationResult<AuthResponse>.Failure(ErrorCodes.AuthInvalidCredentials);
        }

        var user = await _identityService.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return OperationResult<AuthResponse>.Failure(ErrorCodes.AuthInvalidCredentials);
        }

        if (!string.Equals(user.AccountStatus, "Active", StringComparison.Ordinal))
        {
            return OperationResult<AuthResponse>.Failure(ErrorCodes.AuthAccountLocked);
        }

        var response = await BuildAuthResponseAsync(user, context, issuedRefreshToken: null, cancellationToken);
        return OperationResult<AuthResponse>.Success(response);
    }

    /// <summary>Làm mới token theo cơ chế xoay vòng, phát hiện dùng lại token cũ.</summary>
    public async Task<OperationResult<AuthResponse>> RefreshAsync(
        RefreshTokenRequest request,
        RefreshTokenContext context,
        CancellationToken cancellationToken = default)
    {
        var rotation = await _tokenService.RotateRefreshTokenAsync(
            request.RefreshToken,
            context.DeviceName,
            context.IpAddress,
            context.UserAgent,
            cancellationToken);

        if (rotation.Outcome == RefreshOutcome.Reused)
        {
            return OperationResult<AuthResponse>.Failure(ErrorCodes.AuthRefreshTokenReused);
        }

        if (rotation.Outcome == RefreshOutcome.Invalid || rotation.NewToken is null)
        {
            return OperationResult<AuthResponse>.Failure(ErrorCodes.AuthRefreshTokenInvalid);
        }

        var user = await _identityService.GetByIdAsync(rotation.UserId, cancellationToken);
        if (user is null)
        {
            return OperationResult<AuthResponse>.Failure(ErrorCodes.AuthRefreshTokenInvalid);
        }

        // Tài khoản có thể bị khóa sau khi phát refresh token → chặn ngay ở đây.
        if (!string.Equals(user.AccountStatus, "Active", StringComparison.Ordinal))
        {
            return OperationResult<AuthResponse>.Failure(ErrorCodes.AuthAccountLocked);
        }

        var response = await BuildAuthResponseAsync(
            user,
            context,
            rotation.NewToken,
            cancellationToken);

        return OperationResult<AuthResponse>.Success(response);
    }

    /// <summary>Đăng xuất: thu hồi refresh token. Idempotent — gọi lại vẫn thành công.</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await _tokenService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
    }

    /// <summary>
    /// Ghép access token + refresh token thành phản hồi cho client.
    /// Nếu <paramref name="issuedRefreshToken"/> là null thì phát refresh token mới (đăng nhập mới).
    /// </summary>
    private async Task<AuthResponse> BuildAuthResponseAsync(
        IdentityUserData user,
        RefreshTokenContext context,
        RefreshTokenResult? issuedRefreshToken,
        CancellationToken cancellationToken)
    {
        var roles = await _identityService.GetRolesAsync(user.Id, cancellationToken);
        var accessToken = _tokenService.CreateAccessToken(user.Id, user.UserName, roles);

        var refreshToken = issuedRefreshToken ?? await _tokenService.IssueRefreshTokenAsync(
            user.Id,
            familyId: null,
            context.DeviceName,
            context.IpAddress,
            context.UserAgent,
            cancellationToken);

        return new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.Token,
            refreshToken.ExpiresAt,
            new AuthUserDto(user.Id, user.UserName, user.Email, user.DisplayName, roles));
    }
}

/// <summary>
/// Thông tin thiết bị/phiên đi kèm khi phát refresh token.
/// </summary>
public record RefreshTokenContext(string? DeviceName, string? IpAddress, string? UserAgent);
