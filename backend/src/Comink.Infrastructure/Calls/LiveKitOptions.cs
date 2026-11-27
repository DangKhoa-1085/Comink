// Cấu hình nhà cung cấp gọi LiveKit (đọc từ section "LiveKit").
using Microsoft.Extensions.Configuration;

namespace Comink.Infrastructure.Calls;

/// <summary>
/// Cấu hình LiveKit: host HTTP/WS, API key và API secret (chỉ ở backend — dùng để ký token).
/// Giá trị dev khớp `infra/livekit.yaml` và `appsettings.Development.json`.
/// </summary>
public class LiveKitOptions
{
    public const string SectionName = "LiveKit";
    private const string DefaultHost = "http://localhost:7880";

    /// <summary>URL HTTP của LiveKit, ví dụ <c>http://localhost:7880</c>. Backend dùng host này để gọi Twirp.</summary>
    public string Host { get; set; } = DefaultHost;

    /// <summary>
    /// URL client dùng để nối WebSocket (D7). Để trống thì lấy từ <see cref="Host"/>.
    /// Thiết bị thật/emulator đặt <c>ws://&lt;IP-LAN&gt;:7880</c> hoặc <c>ws://10.0.2.2:7880</c>
    /// vì <c>localhost</c> trên điện thoại trỏ về chính điện thoại.
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chỉ ở backend — không bao giờ trả cho client.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>API key đã bỏ khoảng trắng thừa (tránh lỗi khi dán biến môi trường).</summary>
    public string TrimmedApiKey => ApiKey.Trim();

    /// <summary>API secret đã bỏ khoảng trắng thừa (tránh lỗi ký/xác thực JWT).</summary>
    public string TrimmedApiSecret => ApiSecret.Trim();

    /// <summary>Đã cấu hình đủ để gọi hay chưa.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TrimmedApiKey) && !string.IsNullOrWhiteSpace(TrimmedApiSecret);

    /// <summary>
    /// URL HTTP/HTTPS cho backend gọi Twirp API của LiveKit.
    /// Tự chuẩn hoá <c>wss://</c> → <c>https://</c>, <c>ws://</c> → <c>http://</c>; đồng thời giữ
    /// nguyên <c>http://localhost:7880</c> khi dev local cùng <c>PublicUrl=ws://&lt;IP-LAN&gt;</c>.
    /// </summary>
    public string ServerUrl
    {
        get
        {
            var trimmedHost = Host.Trim();
            var trimmedPublicUrl = PublicUrl.Trim();
            var isDefaultHost = trimmedHost.TrimEnd('/')
                .Equals(DefaultHost, StringComparison.OrdinalIgnoreCase);
            var hasCloudPublicUrl =
                trimmedPublicUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) ||
                trimmedPublicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            var value = string.IsNullOrWhiteSpace(trimmedHost) || (isDefaultHost && hasCloudPublicUrl)
                ? (string.IsNullOrWhiteSpace(trimmedPublicUrl) ? DefaultHost : trimmedPublicUrl)
                : trimmedHost;

            return ToHttpScheme(value).TrimEnd('/');
        }
    }

    /// <summary>URL WebSocket trả cho client; ưu tiên <see cref="PublicUrl"/> khi được cấu hình.</summary>
    public string ClientUrl
    {
        get
        {
            var trimmedPublicUrl = PublicUrl.Trim();
            var trimmedHost = Host.Trim();
            var value = !string.IsNullOrWhiteSpace(trimmedPublicUrl)
                ? trimmedPublicUrl
                : !string.IsNullOrWhiteSpace(trimmedHost)
                    ? trimmedHost
                    : DefaultHost;

            return ToWebSocketScheme(value).TrimEnd('/');
        }
    }

    private static string ToHttpScheme(string url) =>
        url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase)
            ? $"https://{url[6..]}"
            : url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                ? $"http://{url[5..]}"
                : url;

    private static string ToWebSocketScheme(string url) =>
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? $"wss://{url[8..]}"
            : url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? $"ws://{url[7..]}"
                : url;

    /// <summary>
    /// Bổ sung giá trị từ bộ biến môi trường chuẩn của LiveKit Cloud
    /// (<c>LIVEKIT_URL</c>, <c>LIVEKIT_API_KEY</c>, <c>LIVEKIT_API_SECRET</c>)
    /// nếu section <c>LiveKit:*</c> chưa khai báo.
    /// </summary>
    public void ApplyEnvironmentFallbacks(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            var envKey = configuration["LIVEKIT_API_KEY"];
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                ApiKey = envKey;
            }
        }

        if (string.IsNullOrWhiteSpace(ApiSecret))
        {
            var envSecret = configuration["LIVEKIT_API_SECRET"];
            if (!string.IsNullOrWhiteSpace(envSecret))
            {
                ApiSecret = envSecret;
            }
        }

        var isHostUnsetOrDefault = string.IsNullOrWhiteSpace(Host) ||
            Host.Trim().TrimEnd('/').Equals(DefaultHost, StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(PublicUrl) && isHostUnsetOrDefault)
        {
            var envUrl = configuration["LIVEKIT_URL"];
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                Host = envUrl;
                PublicUrl = envUrl;
            }
        }
    }
}
