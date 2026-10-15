// DTO của nhóm tính năng Xác thực — hợp đồng với web/app (docs/05_api_contract.md mục 2).
namespace Comink.Application.Identity.Dtos;

/// <summary>Yêu cầu đăng ký tài khoản.</summary>
public record RegisterRequest(
    string UserName,
    string Email,
    string Password,
    string DisplayName);

/// <summary>Yêu cầu đăng nhập bằng email hoặc tên đăng nhập.</summary>
public record LoginRequest(
    string EmailOrUserName,
    string Password,
    string? DeviceName);

/// <summary>Yêu cầu làm mới hoặc thu hồi refresh token.</summary>
public record RefreshTokenRequest(string RefreshToken);

/// <summary>Thông tin người dùng trả kèm kết quả xác thực.</summary>
public record AuthUserDto(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);

/// <summary>Kết quả của đăng ký / đăng nhập / làm mới token.</summary>
public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthUserDto User);
