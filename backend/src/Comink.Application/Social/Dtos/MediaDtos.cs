// DTO và giới hạn của nhóm media (docs/05_api_contract.md mục 7.6).
namespace Comink.Application.Social.Dtos;

/// <summary>Yêu cầu lấy thông tin upload.</summary>
public record MediaSignatureRequest(string Context, string? ResourceType);

/// <summary>Thông tin client cần để tải tệp lên (Cloudinary: chữ ký; Local: endpoint backend).</summary>
public record MediaSignatureDto(
    string Provider,
    string UploadUrl,
    string Signature,
    string ApiKey,
    string CloudName,
    long Timestamp,
    string UploadPreset,
    string Folder);

/// <summary>Kết quả tải tệp lên đĩa cục bộ (chỉ khi dev).</summary>
public record LocalUploadResultDto(
    string Provider,
    string ProviderPublicId,
    string Url,
    string MimeType,
    long SizeBytes);

/// <summary>Đăng ký tài sản media sau khi đã tải lên nơi lưu trữ.</summary>
public record RegisterMediaRequest(
    string ProviderPublicId,
    string Url,
    string? ThumbnailUrl,
    string MimeType,
    long SizeBytes,
    int? Width,
    int? Height,
    string Context);

/// <summary>Tài sản media trả về cho client.</summary>
public record MediaAssetDto(Guid Id, string Url, string? ThumbnailUrl, int? Width, int? Height);

/// <summary>Giới hạn media (kiểm tra ở cả client và server — AGENTS.md mục 8.5).</summary>
public static class MediaLimits
{
    /// <summary>Dung lượng tối đa của một ảnh (5 MB).</summary>
    public const long MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>Định dạng ảnh được phép cho bài viết.</summary>
    public static readonly string[] AllowedImageMimeTypes =
        ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Ngữ cảnh tải lên hợp lệ.</summary>
    public static readonly string[] AllowedContexts = ["Post", "Message", "Avatar", "Cover", "Album"];

    /// <summary>Tệp có phải ảnh được phép không.</summary>
    public static bool IsAllowedImage(string? mimeType) =>
        mimeType is not null && AllowedImageMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);
}
