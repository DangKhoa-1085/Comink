// Tiện ích lấy thông tin người dùng đang đăng nhập từ claims của JWT.
using System.Security.Claims;

namespace Comink.Api.Common;

/// <summary>Đọc dữ liệu người dùng hiện tại từ <see cref="ClaimsPrincipal"/>.</summary>
public static class CurrentUser
{
    /// <summary>
    /// Lấy id người dùng từ claim nameidentifier.
    /// Trả về null nếu token thiếu claim hoặc id không hợp lệ.
    /// </summary>
    public static Guid? GetUserId(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>Địa chỉ IP của client (dùng để lưu vào refresh token).</summary>
    public static string? GetIpAddress(HttpContext? context) => context?.Connection.RemoteIpAddress?.ToString();

    /// <summary>User-Agent của client (dùng để lưu vào refresh token).</summary>
    public static string? GetUserAgent(HttpContext? context)
    {
        var userAgent = context?.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }
}
