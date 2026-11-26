// Tiện ích đọc danh sách origin được phép cho CORS từ cấu hình FrontendUrl.
namespace Comink.Api.Common;

/// <summary>
/// Phân tích chuỗi <c>FrontendUrl</c> (hỗ trợ nhiều origin ngăn cách bởi dấu phẩy hoặc chấm phẩy)
/// thành mảng origin chuẩn hoá để cấu hình CORS.
/// </summary>
public static class CorsOriginsParser
{
    public const string DefaultOrigin = "http://localhost:5173";

    public static string[] Parse(string? rawOrigins)
    {
        if (string.IsNullOrWhiteSpace(rawOrigins))
        {
            return [DefaultOrigin];
        }

        var origins = rawOrigins
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origin => origin.TrimEnd('/'))
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return origins.Length > 0 ? origins : [DefaultOrigin];
    }
}
