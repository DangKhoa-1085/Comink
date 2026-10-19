// Cấu hình nhà cung cấp gọi LiveKit (đọc từ section "LiveKit").
namespace Comink.Infrastructure.Calls;

/// <summary>
/// Cấu hình LiveKit: host HTTP/WS, API key và API secret (chỉ ở backend — dùng để ký token).
/// Giá trị dev khớp `infra/livekit.yaml` và `appsettings.Development.json`.
/// </summary>
public class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    /// <summary>URL HTTP của LiveKit, ví dụ <c>http://localhost:7880</c>. Client dùng cùng host này để nối WS.</summary>
    public string Host { get; set; } = "http://localhost:7880";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chỉ ở backend — không bao giờ trả cho client.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Đã cấu hình đủ để gọi hay chưa.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}
