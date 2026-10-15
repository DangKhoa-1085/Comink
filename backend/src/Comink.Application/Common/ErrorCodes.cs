// Mã lỗi dùng chung giữa backend, web và app.
// Backend trả mã lỗi (tiếng Anh); client tự dịch sang vi/en — xem docs/05_api_contract.md mục 5.
namespace Comink.Application.Common;

/// <summary>
/// Hằng số mã lỗi. Là hợp đồng chung, không đổi giá trị chuỗi khi đã phát hành.
/// </summary>
public static class ErrorCodes
{
    // --- Chung ---
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";

    // --- Xác thực ---
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthAccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string AuthRefreshTokenInvalid = "AUTH_REFRESH_TOKEN_INVALID";
    public const string AuthRefreshTokenReused = "AUTH_REFRESH_TOKEN_REUSED";
    public const string AuthEmailAlreadyExists = "AUTH_EMAIL_ALREADY_EXISTS";
    public const string AuthUserNameAlreadyExists = "AUTH_USERNAME_ALREADY_EXISTS";
}
