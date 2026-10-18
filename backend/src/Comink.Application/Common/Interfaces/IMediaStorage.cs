// Interface kho lưu trữ tệp đa phương tiện (Cloudinary, hoặc đĩa cục bộ khi dev).
namespace Comink.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng lưu trữ media. Cài đặt cụ thể ở Infrastructure:
/// `CloudinaryMediaStorage` (khi có khoá) hoặc `LocalDiskMediaStorage` (khi dev thiếu khoá).
/// </summary>
public interface IMediaStorage
{
    /// <summary>Tên nơi lưu trữ đang dùng ("Cloudinary" hoặc "Local") — ghi vào DB.</summary>
    string Provider { get; }

    /// <summary>
    /// Tạo thông tin để client tải thẳng tệp lên nơi lưu trữ.
    /// Với Cloudinary là chữ ký HMAC; với đĩa cục bộ là endpoint upload của backend.
    /// </summary>
    Task<MediaUploadSignature> GenerateUploadSignatureAsync(
        string context,
        string? folder = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu tệp do client gửi lên backend. Chỉ cài đặt lưu cục bộ hỗ trợ;
    /// Cloudinary trả <see cref="NotSupportedException"/> vì client upload trực tiếp.
    /// </summary>
    Task<StoredMedia> SaveAsync(
        string context,
        string fileName,
        string contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Xóa tệp khỏi nơi lưu trữ theo public ID.</summary>
    Task DeleteAsync(string provider, string publicId, CancellationToken cancellationToken = default);
}

/// <summary>Thông tin client cần để tải tệp lên.</summary>
public record MediaUploadSignature(
    string Provider,
    string UploadUrl,
    string Signature,
    string ApiKey,
    string CloudName,
    long Timestamp,
    string UploadPreset,
    string Folder);

/// <summary>Kết quả lưu một tệp (chỉ cài đặt lưu cục bộ trả về).</summary>
public record StoredMedia(
    string Provider,
    string ProviderPublicId,
    string Url,
    string? ThumbnailUrl,
    string MimeType,
    long SizeBytes);
