// Cấu hình nhà cung cấp gọi LiveKit (đọc từ section "LiveKit").
namespace Comink.Infrastructure.Calls;

/// <summary>
/// Cấu hình LiveKit: host HTTP/WS, API key và API secret (chỉ ở backend — dùng để ký token).
/// Giá trị dev khớp `infra/livekit.yaml` và `appsettings.Development.json`.
/// </summary>
public class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    /// <summary>URL HTTP của LiveKit, ví dụ <c>http://localhost:7880</c>. Backend dùng host này để gọi Twirp.</summary>
    public string Host { get; set; } = "http://localhost:7880";

    /// <summary>
    /// URL client dùng để nối WebSocket (D7). Để trống thì lấy từ <see cref="Host"/>.
    /// Thiết bị thật/emulator đặt <c>ws://&lt;IP-LAN&gt;:7880</c> hoặc <c>ws://10.0.2.2:7880</c>
    /// vì <c>localhost</c> trên điện thoại trỏ về chính điện thoại.
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chỉ ở backend — không bao giờ trả cho client.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Đã cấu hình đủ để gọi hay chưa.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);

    /// <summary>URL WebSocket trả cho client; ưu tiên <see cref="PublicUrl"/> khi được cấu hình.</summary>
    public string ClientUrl
    {
        get
        {
            var value = string.IsNullOrWhiteSpace(PublicUrl) ? Host : PublicUrl;
            return value
                .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');
        }
    }
}
