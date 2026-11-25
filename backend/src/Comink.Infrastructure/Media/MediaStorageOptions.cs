// Cấu hình kho lưu trữ media (đọc từ section "MediaStorage").
namespace Comink.Infrastructure.Media;

/// <summary>
/// Cấu hình lưu trữ media. Có đủ khoá Cloudinary thì dùng Cloudinary;
/// thiếu khoá (máy dev) thì lưu xuống đĩa cục bộ (phương án B của GĐ 2).
/// </summary>
public class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    /// <summary>Tên cloud Cloudinary.</summary>
    public string? CloudName { get; set; }

    /// <summary>Khoá API Cloudinary (công khai phía client).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Khoá bí mật Cloudinary — chỉ ở backend, không bao giờ gửi client.</summary>
    public string? ApiSecret { get; set; }

    /// <summary>Upload preset (unsigned không cần; dùng khi cấu hình signed preset).</summary>
    public string? UploadPreset { get; set; }

    /// <summary>Thư mục gốc lưu tệp khi dùng đĩa cục bộ.</summary>
    public string LocalRootPath { get; set; } = "infra/uploads";

    /// <summary>Tiền tố URL công khai của tệp lưu cục bộ.</summary>
    public string PublicBaseUrl { get; set; } = "/uploads";

    /// <summary>Có đủ cấu hình để dùng Cloudinary hay không.</summary>
    public bool UseCloudinary =>
        !string.IsNullOrWhiteSpace(CloudName)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret);
}
